using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PaymentsApi.Observability;
using PaymentsApi.Payments;

namespace PaymentsApi.Infrastructure.Mongo;

// MongoDB persistence for the payment history. One document per OrderId, enforced by a unique
// index and written with an idempotent upsert (a replayed OrderPlacedEvent updates the same document).
public sealed class MongoPaymentRepository : IPaymentRepository
{
    private readonly IMongoCollection<PaymentRecord> _payments;
    private readonly MongoSettings _settings;
    private readonly ILogger<MongoPaymentRepository> _logger;

    public MongoPaymentRepository(IMongoClient client, IOptions<MongoSettings> options, ILogger<MongoPaymentRepository> logger)
    {
        _settings = options.Value;
        _payments = client.GetDatabase(_settings.DatabaseName).GetCollection<PaymentRecord>(_settings.PaymentsCollectionName);
        _logger = logger;
    }

    public async Task EnsureIndexesAsync(CancellationToken ct = default)
    {
        var keys = Builders<PaymentRecord>.IndexKeys.Ascending(p => p.OrderId);
        var index = new CreateIndexModel<PaymentRecord>(keys, new CreateIndexOptions { Unique = true, Name = "ux_orderId" });
        await _payments.Indexes.CreateOneAsync(index, cancellationToken: ct);
        _logger.LogInformation("MongoDB ready: {Database}.{Collection} with unique index on orderId.",
            _settings.DatabaseName, _settings.PaymentsCollectionName);
    }

    public async Task UpsertAsync(PaymentRecord record, CancellationToken ct = default)
    {
        var filter = Builders<PaymentRecord>.Filter.Eq(p => p.OrderId, record.OrderId);
        var update = Builders<PaymentRecord>.Update
            .SetOnInsert(p => p.Id, record.Id)
            .SetOnInsert(p => p.CreatedAt, record.CreatedAt)
            .Set(p => p.UserId, record.UserId)
            .Set(p => p.GameId, record.GameId)
            .Set(p => p.Price, record.Price)
            .Set(p => p.Status, record.Status)
            .Set(p => p.Reason, record.Reason)
            .Set(p => p.OrderPlacedEventId, record.OrderPlacedEventId)
            .Set(p => p.PaymentProcessedEventId, record.PaymentProcessedEventId)
            .Set(p => p.OrderOccurredAt, record.OrderOccurredAt)
            .Set(p => p.ProcessedAt, record.ProcessedAt)
            .Set(p => p.UpdatedAt, record.UpdatedAt);

        UpdateResult result;
        try
        {
            result = await _payments.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Two upserts for the same order raced on the insert; the unique index rejected one of
            // them. Re-running now takes the update path.
            result = await _payments.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        }

        if (result.UpsertedId is not null)
        {
            FcgMetrics.HistoryWrites.WithLabels("inserted").Inc();
            _logger.LogInformation("Payment history inserted for order {OrderId} ({Status}).", record.OrderId, record.Status);
        }
        else
        {
            FcgMetrics.HistoryWrites.WithLabels("updated").Inc();
            _logger.LogInformation("Payment history updated for order {OrderId} ({Status}); duplicate event handled idempotently.", record.OrderId, record.Status);
        }
    }

    public async Task<PaymentRecord?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => await _payments.Find(p => p.OrderId == orderId).FirstOrDefaultAsync(ct);
}
