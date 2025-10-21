using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using HRS.Domain.Enums;

namespace HRS.Domain.Entities;

public class Payment
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("rentalOrderId")]
    public string RentalOrderId { get; set; } = string.Empty;

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
