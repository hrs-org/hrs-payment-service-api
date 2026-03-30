using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using HRS.API.Contracts.DTOs;
using HRS.API.Contracts.DTOs.Payment;
using HRS.API.Services;
using HRS.API.Services.Interfaces;
using HRS.Domain.Entities;
using HRS.Domain.Enums;
using HRS.Domain.Interfaces;
using HRS.Shared.Core.Dtos;
using HRS.Shared.Core.Interfaces;
using NSubstitute;
using Stripe.Checkout;

namespace HRS.Test.API.Services;

public class PaymentServiceTests
{
    private readonly IAppConfiguration _appConfig;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IUserContextService _userContext;
    private readonly SessionService _sessionService;
    private readonly PaymentService _service;

    public PaymentServiceTests()
    {
        _appConfig = Substitute.For<IAppConfiguration>();
        _paymentRepo = Substitute.For<IPaymentRepository>();
        _userContext = Substitute.For<IUserContextService>();
        _sessionService = Substitute.For<SessionService>();

        var handler = new OrderApiHttpHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient("RentalOrderService").Returns(httpClient);

        _service = new PaymentService(_userContext, _appConfig, httpFactory, _paymentRepo, _sessionService);
    }

    [Fact]
    public async Task CreatePayments_ReturnsSession_AndPostsToHttpClient()
    {
        // Arrange
        _appConfig.StripeApiKey.Returns("sk_test");
        _appConfig.PaymentReturnPath.Returns("payment-returnpage");

        var user = new UserResponseDto { Id = 1, FirstName = "Feri", LastName = "Smith", Role = "Customer", Email = "test@example.com" };
        _userContext.GetUserAsync().Returns(Task.FromResult(user));

        var session = new Session { Id = "sess_123" };
        _sessionService.CreateAsync(Arg.Any<SessionCreateOptions>()).Returns(Task.FromResult(session));

        // Act
        var result = await _service.CreatePayments("order_1");

        // Assert
        result.Should().Be(session);
        await _sessionService.Received(1).CreateAsync(Arg.Is<SessionCreateOptions>(o =>
            o.Metadata != null &&
            o.Metadata.TryGetValue("orderId", out var oid) &&
            oid == "order_1" &&
            o.LineItems!.Count == 1 &&
            o.LineItems[0].PriceData!.UnitAmount == 9999));
    }

    [Fact]
    public async Task VerifyPaymentAsync_ThrowsArgumentException_WhenClientSecretInvalid()
    {
        _appConfig.StripeApiKey.Returns("sk_test");

        await Assert.ThrowsAsync<ArgumentException>(() => _service.VerifyPaymentAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.VerifyPaymentAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.VerifyPaymentAsync("invalid_secret"));
    }

    [Fact]
    public async Task VerifyPaymentAsync_Throws_WhenSessionNotFound()
    {
        _appConfig.StripeApiKey.Returns("sk_test");
        _sessionService.GetAsync(Arg.Any<string>()).Returns((Session?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.VerifyPaymentAsync("cs_abc_def_ghi"));
    }

    [Fact]
    public async Task VerifyPaymentAsync_Throws_WhenPaymentNotCompleted()
    {
        // Arrange
        _appConfig.StripeApiKey.Returns("sk_test");

        var session = new Session
        {
            Id = "sess_123",
            PaymentStatus = "unpaid", // Not "paid"
            Status = "open"           // Not "complete"
        };
        _sessionService.GetAsync(Arg.Any<string>()).Returns(session);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.VerifyPaymentAsync("cs_test_abc_def_ghi")
        );
    }
    [Fact]
    public async Task VerifyPaymentAsync_WhenPaymentCompleted_ApprovesPayment()
    {
        // Arrange
        _appConfig.StripeApiKey.Returns("sk_test");

        var session = new Session
        {
            Id = "sess_123",
            PaymentStatus = "paid",
            Status = "complete",
            AmountTotal = 9999,
            Metadata = new Dictionary<string, string> { ["orderId"] = "order_1" }
        };
        _sessionService.GetAsync(Arg.Any<string>()).Returns(session);

        var user = new UserResponseDto { Id = 1, FirstName = "Feri", LastName = "Smith", Role = "Customer", Email = "test@example.com" };
        _userContext.GetUserAsync().Returns(Task.FromResult(user));

        var handler = new OrderApiHttpHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("RentalOrderService").Returns(httpClient);

        var serviceWithHttp = new PaymentService(_userContext, _appConfig, factory, _paymentRepo, _sessionService);

        // Act
        await serviceWithHttp.VerifyPaymentAsync("cs_test_abc_def_ghi");

        // Assert: no exception; handler returns OK for approve-payment PUT
    }

    [Fact]
    public async Task VerifyPaymentAsync_Throws_WhenPaidAmountDoesNotMatchOrder()
    {
        _appConfig.StripeApiKey.Returns("sk_test");
        var session = new Session
        {
            Id = "sess_123",
            PaymentStatus = "paid",
            Status = "complete",
            AmountTotal = 100,
            Metadata = new Dictionary<string, string> { ["orderId"] = "order_1" }
        };
        _sessionService.GetAsync(Arg.Any<string>()).Returns(session);

        var user = new UserResponseDto { Id = 1, FirstName = "Feri", LastName = "Smith", Role = "Customer", Email = "test@example.com" };
        _userContext.GetUserAsync().Returns(Task.FromResult(user));

        var handler = new OrderApiHttpHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("RentalOrderService").Returns(httpClient);

        var serviceWithHttp = new PaymentService(_userContext, _appConfig, factory, _paymentRepo, _sessionService);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            serviceWithHttp.VerifyPaymentAsync("cs_test_abc_def_ghi"));
    }

    [Fact]
    public async Task VerifyPaymentAsync_Throws_WhenOrderMetadataMissing()
    {
        _appConfig.StripeApiKey.Returns("sk_test");
        var session = new Session
        {
            Id = "sess_123",
            PaymentStatus = "paid",
            Status = "complete",
            AmountTotal = 9999
        };
        _sessionService.GetAsync(Arg.Any<string>()).Returns(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.VerifyPaymentAsync("cs_test_abc_def_ghi"));
    }

    [Fact]
    public async Task AddAsyncPayment_AddsPayment_AndReturnsId()
    {
        // Arrange
        var user = new UserResponseDto { Id = 1, FirstName = "Feri", LastName = "Smith", Role = "Admin", Email = "test@example.com" };
        _userContext.GetUserAsync().Returns(Task.FromResult(user));

        Payment? savedPayment = null;
        _paymentRepo.AddAsync(Arg.Do<Payment>(p => savedPayment = p)).Returns(Task.CompletedTask);

        // Act
        var id = await _service.AddAsyncPayment("order_1", 10000, "sess_123", PaymentType.Stripe, PaymentStatus.Completed);

        // Assert
        id.Should().NotBeNull();
        savedPayment.Should().NotBeNull();
        savedPayment!.RentalOrderId.Should().Be("order_1");
        savedPayment.StripeSessionId.Should().Be("sess_123");
        savedPayment.Amount.Should().Be(100);
        savedPayment.PaymentType.Should().Be(PaymentType.Stripe);
        savedPayment.Status.Should().Be(PaymentStatus.Completed);
    }

    [Fact]
    public async Task UpdateAsyncPayment_UpdatesPayment_AndReturnsId()
    {
        // Arrange
        var user = new UserResponseDto { Id = 1, FirstName = "Feri", LastName = "Smith", Role = "Admin", Email = "test@example.com" };
        _userContext.GetUserAsync().Returns(Task.FromResult(user));

        var existing = new Payment { Id = "pay_1", RentalOrderId = "order_1", Amount = 50 };
        _paymentRepo.GetByRentalOrderIdAsync("order_1").Returns(existing);

        // Act
        var id = await _service.UpdateAsyncPayment("order_1", 15000, "sess_456", PaymentType.Stripe, PaymentStatus.Completed);

        // Assert
        id.Should().Be("pay_1");
        existing.Amount.Should().Be(150);
        existing.StripeSessionId.Should().Be("sess_456");
        existing.PaymentType.Should().Be(PaymentType.Stripe);
        existing.Status.Should().Be(PaymentStatus.Completed);
    }
    /// <summary>GET /api/orders/order_1 returns a pending online order; other calls return 200 OK.</summary>
    private sealed class OrderApiHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri != null &&
                request.RequestUri.AbsolutePath.Contains("/api/orders/", StringComparison.Ordinal))
            {
                var payload = new ApiResponse<RentalOrderForPaymentDto>
                {
                    Success = true,
                    Data = new RentalOrderForPaymentDto
                    {
                        Id = "order_1",
                        TotalAmount = 99.99m,
                        Status = "PendingPayment",
                        CustomerId = 1,
                        PaymentType = "Other"
                    }
                };
                var json = JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

}



public class PaymentServiceRepositoryTests
{
    private readonly IPaymentRepository _paymentRepo;
    private readonly IUserContextService _userContext;
    private readonly PaymentService _service;

    public PaymentServiceRepositoryTests()
    {
        _paymentRepo = Substitute.For<IPaymentRepository>();
        _userContext = Substitute.For<IUserContextService>();

        // Minimal dependencies needed for these methods
        _service = new PaymentService(
            _userContext,
            Substitute.For<IAppConfiguration>(),
            Substitute.For<IHttpClientFactory>(),
            _paymentRepo,
            Substitute.For<SessionService>()
        );
    }

    [Fact]
    public async Task MongoDBGet_ReturnsPayment_WhenFound()
    {
        // Arrange
        var payment = new Payment { Id = "pay_1" };
        _paymentRepo.GetByIdAsync("pay_1")!.Returns(Task.FromResult(payment));

        // Act
        var result = await _service.MongoDBGet("pay_1");

        // Assert
        result.Should().Be(payment);
    }

    [Fact]
    public async Task MongoDBGet_ThrowsInvalidOperation_WhenNotFound()
    {
        // Arrange
        _paymentRepo.GetByIdAsync("pay_1")!.Returns(Task.FromResult<Payment>(null!));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.MongoDBGet("pay_1"));
    }

    [Fact]
    public async Task GetByOrderId_ReturnsPayment_WhenFound()
    {
        // Arrange
        var payment = new Payment { RentalOrderId = "order_1" };
        _paymentRepo.GetByRentalOrderIdAsync("order_1")!.Returns(Task.FromResult(payment));

        // Act
        var result = await _service.GetByOrderId("order_1");

        // Assert
        result.Should().Be(payment);
    }

    [Fact]
    public async Task GetByOrderId_ReturnsNull_WhenNotFound()
    {
        // Arrange
        _paymentRepo.GetByRentalOrderIdAsync("order_1")!.Returns(Task.FromResult<Payment>(null!));

        // Act
        var result = await _service.GetByOrderId("order_1");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RecordPayment_AddsPayment_WhenNotExists()
    {
        // Arrange
        _paymentRepo.GetByRentalOrderIdAsync("order_1")!.Returns(Task.FromResult<Payment>(null!));

        var user = new UserResponseDto { Id = 42, FirstName = "Feri", LastName = "Smith", Role = "Admin", Email = "test@example.com" };
        _userContext.GetUserAsync().Returns(Task.FromResult(user));

        Payment? savedPayment = null;
        _paymentRepo.AddAsync(Arg.Do<Payment>(p => savedPayment = p)).Returns(Task.CompletedTask);

        // Act
        await _service.RecordPayment("order_1", 10000, "sess_123", PaymentType.Stripe);

        // Assert
        savedPayment.Should().NotBeNull();
        savedPayment!.RentalOrderId.Should().Be("order_1");
        savedPayment.StripeSessionId.Should().Be("sess_123");
        savedPayment.Amount.Should().Be(100); // 10000 / 100
        savedPayment.PaymentType.Should().Be(PaymentType.Stripe);
        savedPayment.Status.Should().Be(PaymentStatus.Completed);
        savedPayment.CreatedById.Should().Be(42);
    }

    [Fact]
    public async Task RecordPayment_DoesNothing_WhenPaymentExists()
    {
        // Arrange
        var existing = new Payment { Id = "pay_1" };
        _paymentRepo.GetByRentalOrderIdAsync("order_1").Returns(existing);

        // Act
        await _service.RecordPayment("order_1", 10000, "sess_123", PaymentType.Stripe);

        // Assert
        await _paymentRepo.DidNotReceive().AddAsync(Arg.Any<Payment>());
    }
}



