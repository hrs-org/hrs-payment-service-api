using HRS.API.Contracts.DTOs;
using HRS.API.Contracts.DTOs.Payment;
using HRS.API.Services.Interfaces;
using HRS.Domain.Enums;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;

namespace HRS.API.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("create-session")]
    public async Task<ActionResult> GetAvailability([FromBody] PaymentRequestDto request)
    {
        var hashedUserId = GetHashedUserId();

        _logger.LogInformation(
            "Payment create-session requested order_id={OrderId} amount={Amount} user_id={UserId}",
            request.OrderId,
            request.Amount,
            hashedUserId);

        var result = await _paymentService.CreatePayments(request.OrderId, request.Amount);

        _logger.LogInformation(
            "Payment create-session succeeded order_id={OrderId} session_id_present={SessionIdPresent}",
            request.OrderId,
            !string.IsNullOrWhiteSpace(result.Id));

        return Ok(ApiResponse<Session>.OkResponse(result));
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequestDto request)
    {
        var hashedUserId = GetHashedUserId();

        _logger.LogInformation(
            "Payment verify requested secret_key_present={SecretKeyPresent} user_id={UserId}",
            !string.IsNullOrWhiteSpace(request.SecretKey),
            hashedUserId);

        try
        {
            await _paymentService.VerifyPaymentAsync(request.SecretKey);
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning(
                "event_type={EventType} user_id={UserId} secret_key_present={SecretKeyPresent}",
                "payment.declined",
                hashedUserId,
                !string.IsNullOrWhiteSpace(request.SecretKey));
            throw;
        }
        catch (Exception)
        {
            _logger.LogError(
                "event_type={EventType} user_id={UserId} secret_key_present={SecretKeyPresent}",
                "payment.failed",
                hashedUserId,
                !string.IsNullOrWhiteSpace(request.SecretKey));
            throw;
        }

        _logger.LogInformation("Payment verify succeeded user_id={UserId}", hashedUserId);
        return Ok(ApiResponse<object>.OkResponse(null, "Payment verified successfully"));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "read:payment")]

    public async Task<IActionResult> MongoDBGet(string id)
    {
        var hashedUserId = GetHashedUserId();
        _logger.LogInformation(
            "event_type={EventType} user_id={UserId} payment_id={PaymentId}",
            "payment.sensitive_access",
            hashedUserId,
            id);

        var payment = await _paymentService.MongoDBGet(id);
        return Ok(ApiResponse<object>.OkResponse(payment, "GET Payment successfully"));
    }

    [HttpGet("orders/{id}")]
    [Authorize(Policy = "read:payment")]
    public async Task<IActionResult> GetByOrderId(string id)
    {
        var hashedUserId = GetHashedUserId();
        _logger.LogInformation(
            "event_type={EventType} user_id={UserId} order_id={OrderId}",
            "payment.sensitive_access",
            hashedUserId,
            id);

        var payment = await _paymentService.GetByOrderId(id);
        return Ok(ApiResponse<object>.OkResponse(payment, "GET Payment successfully"));
    }

    [HttpPost]
    public async Task<IActionResult> AddAsyncPayment([FromBody] CreatePaymentRequestDto request)
    {
        _logger.LogInformation(
            "Payment add requested order_id={OrderId} amount={Amount} status={Status} type={PaymentType}",
            request.OrderId,
            request.Amount,
            request.Status,
            request.PaymentType);

        var payment = await _paymentService.AddAsyncPayment(request.OrderId, request.Amount, request.SessionId, request.PaymentType, request.Status);

        _logger.LogInformation("Payment add succeeded order_id={OrderId}", request.OrderId);
        return Ok(ApiResponse<object>.OkResponse(payment, "Payment added successfully"));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateAsyncPayment([FromBody] CreatePaymentRequestDto request)
    {
        _logger.LogInformation(
            "Payment update requested order_id={OrderId} amount={Amount} status={Status} type={PaymentType}",
            request.OrderId,
            request.Amount,
            request.Status,
            request.PaymentType);

        var payment = await _paymentService.UpdateAsyncPayment(request.OrderId, request.Amount, request.SessionId, request.PaymentType, request.Status);

        _logger.LogInformation("Payment update succeeded order_id={OrderId}", request.OrderId);
        return Ok(ApiResponse<object>.OkResponse(payment, "Payment updated successfully"));
    }

    private string GetHashedUserId()
    {
        var subject = HttpContext?.User?.FindFirst("sub")?.Value ?? "anonymous";
        return HashIdentifier(subject);
    }

    private static string HashIdentifier(string value)
    {
        var inputBytes = Encoding.UTF8.GetBytes(value);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes)[..16];
    }
}
