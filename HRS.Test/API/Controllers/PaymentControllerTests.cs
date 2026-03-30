// using System.Threading.Tasks;
using FluentAssertions;
using HRS.API.Contracts.DTOs.Payment;
using HRS.API.Controllers;
using HRS.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe.Checkout;
using Xunit;
using HRS.Domain.Enums;
using HRS.Domain.Entities;

namespace HRS.Tests.Controllers;

public class PaymentControllerTests
{
    private readonly PaymentController _controller;
    private readonly IPaymentService _service;

    public PaymentControllerTests()
    {
        _service = Substitute.For<IPaymentService>();
        var logger = Substitute.For<ILogger<PaymentController>>();
        _controller = new PaymentController(_service, logger);
    }

    [Fact]
    public async Task GetAvailability_ReturnsOkWithSession()
    {
        var request = new PaymentRequestDto { OrderId = "1", Amount = 100 };
        var session = new Session { Id = "sess_123" };
        _service.CreatePayments(request.OrderId, request.Amount).Returns(session);

        var result = await _controller.GetAvailability(request);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        var apiResponse = okResult!.Value as dynamic;
        ((Session)apiResponse?.Data!).Should().BeEquivalentTo(session);
    }

    [Fact]
    public async Task VerifyPayment_ReturnsOkWithApiResponse()
    {
        var request = new VerifyPaymentRequestDto { SecretKey = "sk_test" };
        _service.VerifyPaymentAsync(request.SecretKey).Returns(Task.CompletedTask);

        var result = await _controller.VerifyPayment(request);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        var apiResponse = okResult!.Value as dynamic;
        ((object)apiResponse?.Data!).Should().BeNull();
        ((string)apiResponse?.Message!).Should().Be("Payment verified successfully");
        await _service.Received(1).VerifyPaymentAsync(request.SecretKey);
    }

    [Fact]
    public async Task MongoDBGet_ReturnsOkWithPayment()
    {
        var id = "payment1";
        var paymentObj = new Payment { Id = id };
        _service.MongoDBGet(id).Returns(paymentObj);

        var result = await _controller.MongoDBGet(id);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        var apiResponse = okResult!.Value as dynamic;
        ((object)apiResponse?.Data!).Should().BeEquivalentTo(paymentObj);
        ((string)apiResponse?.Message!).Should().Be("GET Payment successfully");
    }

    [Fact]
    public async Task GetByOrderId_ReturnsOkWithPayment()
    {
        var orderId = "order1";
        var paymentObj = new Payment { RentalOrderId = orderId };
        _service.GetByOrderId(orderId).Returns(paymentObj);

        var result = await _controller.GetByOrderId(orderId);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        var apiResponse = okResult!.Value as dynamic;
        ((object)apiResponse?.Data!).Should().BeEquivalentTo(paymentObj);
        ((string)apiResponse?.Message!).Should().Be("GET Payment successfully");
    }

    [Fact]
    public async Task AddAsyncPayment_ReturnsOkWithPayment()
    {
        var request = new CreatePaymentRequestDto
        {
            OrderId = "ORD123",
            Amount = 1000,
            SessionId = "sess_1",
            PaymentType = PaymentType.Cash,
            Status = PaymentStatus.Pending
        };
        var paymentObj = new Payment { Id = "pay_1" };
        _service.AddAsyncPayment(request.OrderId, request.Amount, request.SessionId, request.PaymentType, request.Status)
                .Returns(paymentObj.Id);

        var result = await _controller.AddAsyncPayment(request);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        var apiResponse = okResult!.Value as dynamic;
        ((object)apiResponse?.Data!).Should().BeEquivalentTo(paymentObj.Id);
        ((string)apiResponse?.Message!).Should().Be("Payment added successfully");
    }

    [Fact]
    public async Task UpdateAsyncPayment_ReturnsOkWithPayment()
    {
        var request = new CreatePaymentRequestDto
        {
            OrderId = "ORD123",
            Amount = 1000,
            SessionId = "sess_1",
            PaymentType = PaymentType.Cash,
            Status = PaymentStatus.Pending
        };
        var paymentObj = new Payment { Id = "pay_1" };
        _service.UpdateAsyncPayment(request.OrderId, request.Amount, request.SessionId, request.PaymentType, request.Status)
                .Returns(paymentObj.Id);

        var result = await _controller.UpdateAsyncPayment(request);

        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        var apiResponse = okResult!.Value as dynamic;
        ((object)apiResponse?.Data!).Should().BeEquivalentTo(paymentObj.Id);
        ((string)apiResponse?.Message!).Should().Be("Payment updated successfully");
    }
}
