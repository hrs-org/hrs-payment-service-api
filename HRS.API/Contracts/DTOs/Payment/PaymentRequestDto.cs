using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRS.Domain.Enums;

namespace HRS.API.Contracts.DTOs.Payment;

public class PaymentRequestDto
{
    [Required] public string OrderId { get; set; } = string.Empty;

    /// <summary>Ignored by the server; the checkout total is taken from the order record to prevent tampering.</summary>
    public double? Amount { get; set; }
}

public class VerifyPaymentRequestDto
{
    [Required] public string SecretKey { get; set; } = string.Empty;
}

public class CreatePaymentRequestDto
{
    [Required] public string OrderId { get; set; } = string.Empty;
    [Required] public long Amount { get; set; }
    [Required] public string? SessionId { get; set; }
    [Required][JsonConverter(typeof(JsonStringEnumConverter))] public PaymentType PaymentType { get; set; }
    [Required][JsonConverter(typeof(JsonStringEnumConverter))] public PaymentStatus Status { get; set; }
}

public class ApprovePaymentRequestDto
{
    [Required] public string? SessionId { get; set; }
    [Required] public long Amount { get; set; }

}
