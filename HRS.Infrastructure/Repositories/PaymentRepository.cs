using HRS.Domain.Entities;
using HRS.Domain.Interfaces;
using MongoDB.Driver;

namespace HRS.Infrastructure.Repositories;

public class PaymentRepository : CrudRepository<Payment>, IPaymentRepository
{
    private readonly IMongoCollection<Payment> _payments;
    public PaymentRepository(MongoContext context)
        : base(context, "Payments")
    {
         _payments = context.Payments;

        var indexKeys = Builders<Payment>.IndexKeys.Ascending(p => p.RentalOrderId);
        var indexModel = new CreateIndexModel<Payment>(indexKeys, new CreateIndexOptions { Unique = true });
        _payments.Indexes.CreateOne(indexModel);
    }

    public async Task<Payment?> GetByRentalOrderIdAsync(int rentalOrderId)
    {
        var filter = Builders<Payment>.Filter.Eq(p => p.RentalOrderId, rentalOrderId);
        return await _collection.Find(filter).FirstOrDefaultAsync();
    }
}

