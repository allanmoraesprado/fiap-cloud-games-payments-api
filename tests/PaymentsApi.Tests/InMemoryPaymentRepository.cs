using PaymentsApi.Payments;

namespace PaymentsApi.Tests;

// Test double with the same contract as MongoPaymentRepository: one record per OrderId,
// insert on first sight, update afterwards (Id and CreatedAt preserved).
public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly Dictionary<Guid, PaymentRecord> _byOrder = new();

    public int Count => _byOrder.Count;
    public PaymentRecord? Get(Guid orderId) => _byOrder.GetValueOrDefault(orderId);

    public Task UpsertAsync(PaymentRecord record, CancellationToken ct = default)
    {
        if (_byOrder.TryGetValue(record.OrderId, out var existing))
        {
            existing.UserId = record.UserId;
            existing.GameId = record.GameId;
            existing.Price = record.Price;
            existing.Status = record.Status;
            existing.Reason = record.Reason;
            existing.OrderPlacedEventId = record.OrderPlacedEventId;
            existing.PaymentProcessedEventId = record.PaymentProcessedEventId;
            existing.OrderOccurredAt = record.OrderOccurredAt;
            existing.ProcessedAt = record.ProcessedAt;
            existing.UpdatedAt = record.UpdatedAt;
        }
        else
        {
            _byOrder[record.OrderId] = record;
        }
        return Task.CompletedTask;
    }

    public Task<PaymentRecord?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => Task.FromResult(_byOrder.GetValueOrDefault(orderId));
}
