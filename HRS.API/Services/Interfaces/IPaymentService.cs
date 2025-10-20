using HRS.Domain.Entities;
using HRS.Domain.Enums;
using Stripe.Checkout;

namespace HRS.API.Services.Interfaces;

public interface IPaymentService
{
    Task<Session> CreatePayments(int orderId, double amount);
    Task VerifyPaymentAsync(string clientSecret);

    Task RecordPayment(int orderId, long? amount, string? sessionId, PaymentType paymentType);
    Task<String> AddAsyncPayment(int orderId, long? amount, string? sessionId, PaymentType paymentType, PaymentStatus status);
    Task<Payment> MongoDBGet(string id);
    Task<Payment> GetByOrderId(int id);
    Task<String> UpdateAsyncPayment(int orderId, long? amount, string? sessionId, PaymentType paymentType, PaymentStatus status);
}
