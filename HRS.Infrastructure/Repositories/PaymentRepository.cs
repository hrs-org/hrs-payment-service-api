using HRS.Domain.Entities;
using HRS.Domain.Interfaces;
using MongoDB.Driver;

namespace HRS.Infrastructure.Repositories;

public class PaymentRepository : CrudRepository<Payment>, IPaymentRepository
{
    public PaymentRepository(MongoContext context)
        : base(context, "Payments") { }

    public async Task<Payment?> GetByRentalOrderIdAsync(int rentalOrderId)
    {
        var filter = Builders<Payment>.Filter.Eq(p => p.RentalOrderId, rentalOrderId);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }
}

