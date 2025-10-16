using HRS.Domain.Entities;
using HRS.Domain.Enums;
using Stripe.Checkout;

namespace HRS.API.Services.Interfaces;

public interface IPaymentService
{
    Task<Session> CreatePayments(int orderId, double amount);
    Task VerifyPaymentAsync(string clientSecret);

    Task RecordPayment(int orderId, long? amount, string? sessionId, PaymentType paymentType);
    Task<String> TestMongoDB(int orderId, long? amount, string? sessionId, PaymentType paymentType);
    Task<Payment> TestMongoDBGET(string ID);
    Task<Payment> TestMongoDBGETbyOrderID(int ID);
}
