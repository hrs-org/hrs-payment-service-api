namespace HRS.API.Contracts.DTOs.Payment;

/// <summary>
/// Minimal order shape returned by the order service for payment authorization and amount checks.
/// </summary>
public sealed class RentalOrderForPaymentDto
{
    public string Id { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public string PaymentType { get; set; } = string.Empty;
}
