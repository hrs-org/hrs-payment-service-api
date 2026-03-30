using System.Net.Http.Json;
using HRS.API.Contracts.DTOs;
using HRS.API.Contracts.DTOs.Payment;
using HRS.API.Services.Interfaces;
using HRS.Domain.Entities;
using HRS.Domain.Enums;
using HRS.Domain.Interfaces;
using HRS.Shared.Core.Dtos;
using HRS.Shared.Core.Interfaces;
using Stripe;
using Stripe.Checkout;

namespace HRS.API.Services;

public class PaymentService : IPaymentService
{
    private readonly IAppConfiguration _appConfiguration;
    private readonly IPaymentRepository _paymentRepository;
    private readonly SessionService _sessionService;
    private readonly IUserContextService _userContextService;

    private readonly HttpClient _httpClient;

    public PaymentService(
        IUserContextService userContextService,
        IAppConfiguration appConfiguration,
        IHttpClientFactory httpClientFactory,
        IPaymentRepository paymentRepository,
        SessionService? sessionService = null)
    {
        _userContextService = userContextService;
        _appConfiguration = appConfiguration;
        _httpClient = httpClientFactory.CreateClient("RentalOrderService");
        _paymentRepository = paymentRepository;

        // Use the provided service for testing, fallback to real service for production
        _sessionService = sessionService ?? new SessionService();
    }

    public async Task<Session> CreatePayments(string orderId)
    {
        StripeConfiguration.ApiKey = _appConfiguration.StripeApiKey;
        var user = await _userContextService.GetUserAsync();
        var order = await FetchOrderForPaymentAsync(orderId);
        EnsureOrderAwaitingPayment(order);
        EnsureUserCanInitiatePayment(user, order);

        var unitAmountMinor = OrderTotalToMinorUnits(order.TotalAmount);
        if (unitAmountMinor <= 0)
            throw new InvalidOperationException("Order total is invalid for checkout.");

        var id = user.Id.ToString();
        var orderName = "OrderID:" + orderId + "-User:" + id;

        var options = new SessionCreateOptions
        {
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = unitAmountMinor,
                        Currency = "SGD",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = orderName
                        }
                    },
                    Quantity = 1
                }
            },
            Mode = "payment",
            CustomerEmail = user.Email,
            UiMode = "embedded",
            ReturnUrl = _appConfiguration.PaymentReturnPath,
            ExpiresAt = DateTime.UtcNow.AddMinutes(35),
            Metadata = new Dictionary<string, string> { ["orderId"] = orderId }
        };

        var session = await _sessionService.CreateAsync(options);
        var response = await _httpClient.PostAsJsonAsync($"/api/orders/assign-stripe-sessionid/{orderId}", new { orderId, sessionId = session.Id });
        response.EnsureSuccessStatusCode();

        return session;
    }

    public async Task VerifyPaymentAsync(string clientSecret)
    {
        StripeConfiguration.ApiKey = _appConfiguration.StripeApiKey;
        if (string.IsNullOrEmpty(clientSecret))
            throw new ArgumentException("clientSecret is required");

        var parts = clientSecret.Split('_');
        if (parts.Length < 4)
            throw new ArgumentException("Invalid clientSecret format");

        var sessionId = string.Join("_", parts[0], parts[1], parts[2]);

        var session = await _sessionService.GetAsync(sessionId) ?? throw new InvalidOperationException("Invalid Stripe session.");

        if (session.PaymentStatus != "paid" || session.Status != "complete")
            throw new InvalidOperationException("Payment not completed.");

        if (session.Metadata == null ||
            !session.Metadata.TryGetValue("orderId", out var metadataOrderId) ||
            string.IsNullOrWhiteSpace(metadataOrderId))
            throw new InvalidOperationException("Checkout session is missing order metadata.");

        var order = await FetchOrderForPaymentAsync(metadataOrderId);
        var user = await _userContextService.GetUserAsync();
        EnsureUserCanInitiatePayment(user, order);

        var expectedMinor = OrderTotalToMinorUnits(order.TotalAmount);
        if (!session.AmountTotal.HasValue || session.AmountTotal.Value != expectedMinor)
            throw new InvalidOperationException("Paid amount does not match the order total.");

        var res = await _httpClient.PutAsJsonAsync($"/api/orders/{sessionId}/approve-payment",
            new { sessionId, amount = session.AmountTotal });
        res.EnsureSuccessStatusCode();
    }

    private async Task<RentalOrderForPaymentDto> FetchOrderForPaymentAsync(string orderId)
    {
        var response = await _httpClient.GetAsync($"/api/orders/{Uri.EscapeDataString(orderId)}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new KeyNotFoundException("Order not found.");

        response.EnsureSuccessStatusCode();
        var wrapped = await response.Content.ReadFromJsonAsync<ApiResponse<RentalOrderForPaymentDto>>();
        if (wrapped?.Data == null)
            throw new KeyNotFoundException("Order not found.");

        if (!string.Equals(wrapped.Data.Id, orderId, StringComparison.Ordinal))
            throw new InvalidOperationException("Order response does not match requested order.");

        return wrapped.Data;
    }

    private static long OrderTotalToMinorUnits(decimal totalAmount) =>
        (long)Math.Round(totalAmount * 100m, 0, MidpointRounding.AwayFromZero);

    private static void EnsureOrderAwaitingPayment(RentalOrderForPaymentDto order)
    {
        if (!string.Equals(order.Status, "PendingPayment", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Order is not awaiting payment (status: {order.Status}).");
    }

    private static void EnsureUserCanInitiatePayment(UserResponseDto user, RentalOrderForPaymentDto order)
    {
        if (string.Equals(user.Role, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            if (order.CustomerId != user.Id)
                throw new UnauthorizedAccessException("You cannot pay for this order.");
        }
    }

    public async Task RecordPayment(string orderId, long? amount, string? sessionId, PaymentType paymentType)
    {
        var existingPayments = await _paymentRepository
            .GetByRentalOrderIdAsync(orderId);

        if (existingPayments != null)
            return;

        var user = await _userContextService.GetUserAsync();

        var payment = new Payment
        {
            RentalOrderId = orderId,
            StripeSessionId = sessionId,
            Amount = (decimal)(amount ?? 0) / 100,
            PaymentType = paymentType,
            PaymentDate = DateTime.UtcNow,
            Status = PaymentStatus.Completed,
            CreatedById = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        await _paymentRepository.AddAsync(payment);
    }

    public async Task<String> AddAsyncPayment(string orderId, long? amount, string? sessionId, PaymentType paymentType, PaymentStatus status)
    {

        var user = await _userContextService.GetUserAsync();

        var payment = new Payment
        {
            RentalOrderId = orderId,
            StripeSessionId = sessionId,
            Amount = (decimal)(amount ?? 0) / 100,
            PaymentType = paymentType,
            PaymentDate = DateTime.UtcNow,
            Status = status,
            CreatedById = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedById = user.Id,
            UpdatedAt = DateTime.UtcNow
        };
        await _paymentRepository.AddAsync(payment);
        if (payment.Id == null) throw new InvalidOperationException("Null id");

        return payment.Id;
    }

    public async Task<String> UpdateAsyncPayment(string orderId, long? amount, string? sessionId, PaymentType paymentType, PaymentStatus status)
    {

        var user = await _userContextService.GetUserAsync();
        var payment = await _paymentRepository.GetByRentalOrderIdAsync(orderId);

        if (payment == null) throw new InvalidOperationException("Payment not found");
        payment.RentalOrderId = orderId;
        payment.StripeSessionId = sessionId;
        payment.Amount = (decimal)(amount ?? 0) / 100;
        payment.PaymentType = paymentType;
        payment.PaymentDate = DateTime.UtcNow;
        payment.Status = status;
        payment.UpdatedById = user.Id;
        payment.UpdatedAt = DateTime.UtcNow;

        await _paymentRepository.UpdateAsync(payment, payment.Id);
        return payment.Id;
    }

    public async Task<Payment> MongoDBGet(string id)
    {

        var data = await _paymentRepository.GetByIdAsync(id);

        if (data == null) throw new InvalidOperationException("ERROR");

        return data;
    }

    public async Task<Payment?> GetByOrderId(string orderId)
    {

        var data = await _paymentRepository.GetByRentalOrderIdAsync(orderId);
        return data;
    }
}
