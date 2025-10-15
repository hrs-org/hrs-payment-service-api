using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using HRS.Domain.Enums;

namespace HRS.Domain.Entities;

public class Payment
{
    [BsonId] // primary key
    [BsonRepresentation(BsonType.Int32)]
    public int Id { get; set; }

    [BsonElement("rentalOrderId")]
    public int RentalOrderId { get; set; }

    [BsonElement("amount")]
    public decimal Amount { get; set; }

    [BsonElement("paymentType")]
    public PaymentType PaymentType { get; set; } = PaymentType.Stripe;

    [BsonElement("status")]
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [BsonElement("stripeSessionId")]
    public string? StripeSessionId { get; set; }

    [BsonElement("stripePaymentIntentId")]
    public string? StripePaymentIntentId { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("paymentDate")]
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [BsonElement("createdById")]
    public int? CreatedById { get; set; }

    [BsonElement("updatedById")]
    public int? UpdatedById { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}
