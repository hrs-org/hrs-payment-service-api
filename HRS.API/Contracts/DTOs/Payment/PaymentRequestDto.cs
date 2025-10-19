using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRS.Domain.Enums;

namespace HRS.API.Contracts.DTOs.Payment;

public class PaymentRequestDto
{
    [Required] public int OrderId { get; set; }
    [Required] public double Amount { get; set; }
}

public class VerifyPaymentRequestDto
{
    [Required] public string SecretKey { get; set; } = string.Empty;
}

public class CreatePaymentRequestDto
{
    [Required] public int OrderId { get; set; }
    [Required] public long Amount { get; set; }
    [Required] public string? SessionId { get; set; }
    [Required] [JsonConverter(typeof(JsonStringEnumConverter))] public PaymentType PaymentType { get; set; }
}
