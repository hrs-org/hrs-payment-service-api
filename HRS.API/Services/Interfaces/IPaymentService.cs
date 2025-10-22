using HRS.Domain.Entities;
using HRS.Domain.Enums;
using Stripe.Checkout;

namespace HRS.API.Services.Interfaces;

public interface IPaymentService
{
    Task<Session> CreatePayments(string orderId, double amount);
    Task VerifyPaymentAsync(string clientSecret);

    Task RecordPayment(string orderId, long? amount, string? sessionId, PaymentType paymentType);
    Task<String> AddAsyncPayment(string orderId, long? amount, string? sessionId, PaymentType paymentType, PaymentStatus status);
    Task<Payment> MongoDBGet(string id);
    Task<Payment?> GetByOrderId(string orderId);
    Task<String> UpdateAsyncPayment(string orderId, long? amount, string? sessionId, PaymentType paymentType, PaymentStatus status);
}
