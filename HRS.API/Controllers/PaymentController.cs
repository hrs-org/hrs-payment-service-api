using HRS.API.Contracts.DTOs;
using HRS.API.Contracts.DTOs.Payment;
using HRS.API.Services.Interfaces;
using HRS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;

namespace HRS.API.Controllers;

[ApiController]
[Route("api/payments")]
// [Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("create-session")]
    public async Task<ActionResult> GetAvailability([FromBody] PaymentRequestDto request)
    {
        var result = await _paymentService.CreatePayments(request.OrderId, request.Amount);
        return Ok(ApiResponse<Session>.OkResponse(result));
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequestDto request)
    {
        await _paymentService.VerifyPaymentAsync(request.SecretKey);
        return Ok(ApiResponse<object>.OkResponse(null, "Payment verified successfully"));
    }

    [HttpGet("{ID}")]
    public async Task<IActionResult> MongoDBGET(string ID)
    {
        var payment = await _paymentService.MongoDBGET(ID);
        return Ok(ApiResponse<object>.OkResponse(payment, "GET Payment successfully"));
    }

    [HttpGet("orders/{Id}")]
    public async Task<IActionResult> GETByOrderID(int Id)
    {
        var payment = await _paymentService.GETbyOrderID(Id);
        return Ok(ApiResponse<object>.OkResponse(payment, "GET Payment successfully"));
    }

    [HttpPost]
    public async Task<IActionResult> AddAsyncPayment([FromBody] CreatePaymentRequestDto request)
    {
        var payment = await _paymentService.AddAsyncPayment(request.OrderId, request.Amount, request.SessionId, request.PaymentType, request.Status);
        return Ok(ApiResponse<object>.OkResponse(payment, "Payment added successfully"));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateAsyncPayment([FromBody] CreatePaymentRequestDto request)
    {
        var payment = await _paymentService.UpdateAsyncPayment(request.OrderId, request.Amount, request.SessionId, request.PaymentType, request.Status);
        return Ok(ApiResponse<object>.OkResponse(payment, "Payment updated successfully"));
    }

}
