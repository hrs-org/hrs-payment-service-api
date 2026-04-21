using HRS.API.Services.Interfaces;
using HRS.Domain.Entities;
using HRS.Domain.Enums;
using HRS.Domain.Interfaces;
using HRS.API.Contracts.DTOs;
using HRS.Shared.Core.Interfaces;
using Stripe;
using Stripe.Checkout;
using System.Net.Http.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

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

    public async Task<Session> CreatePayments(string orderId, double amount)
    {
        StripeConfiguration.ApiKey = _appConfiguration.StripeApiKey;

        var order = await GetOrderSecurityCheckAsync(orderId);
        ValidateStoreOwnership(order.StoreId);
        // Payment-service Domain does not include RentalStatus enum.
        // The Order API returns Status as string (e.g. "PendingPayment").
        if (!string.Equals(order.Status, "PendingPayment", StringComparison.Ordinal))
            throw new InvalidOperationException("Only pending payment orders can be paid.");

        var user = await _userContextService.GetUserAsync();
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
                        UnitAmount = (long)(amount * 100),
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
            ExpiresAt = DateTime.UtcNow.AddMinutes(35)
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

        if (session.Status == "complete")
        {
            var res = await _httpClient.PutAsJsonAsync($"/api/orders/{sessionId}/approve-payment", new { sessionId, amount = session.AmountTotal });
            res.EnsureSuccessStatusCode();
            await Task.CompletedTask;
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

    private async Task<OrderSecurityCheckDto> GetOrderSecurityCheckAsync(string orderId)
    {
        // Security: validate order/store before allowing Stripe checkout creation.
        // This endpoint is protected by JWT and will include current caller's auth context.
        var response = await _httpClient.GetFromJsonAsync<ApiResponse<OrderSecurityCheckDto>>($"/api/orders/{orderId}");
        if (response?.Data == null)
            throw new InvalidOperationException("Order not found.");
        return response.Data;
    }

    [SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Used by System.Text.Json deserialization via GetFromJsonAsync.")]
    private sealed class OrderSecurityCheckDto
    {
        public int StoreId { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public async Task<String> AddAsyncPayment(string orderId, long? amount, string? sessionId, PaymentType paymentType, PaymentStatus status)
    {
        var order = await GetOrderSecurityCheckAsync(orderId);
        ValidateStoreOwnership(order.StoreId);

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
        var order = await GetOrderSecurityCheckAsync(orderId);
        ValidateStoreOwnership(order.StoreId);

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

    private void ValidateStoreOwnership(int orderStoreId)
    {
        var storeId = _userContextService.GetStoreId();

        // Customers may not have storeId claim (returns 0), so skip store ownership check for them.
        if (storeId <= 0)
            return;

        if (orderStoreId != storeId)
            throw new InvalidOperationException("Order does not belong to your store.");
    }
}
