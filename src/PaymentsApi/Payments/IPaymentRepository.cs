namespace PaymentsApi.Payments;

public interface IPaymentRepository
{
    // Idempotent: one document per OrderId. Inserts on first sight; a replayed event updates
    // the same document (createdAt is kept, updatedAt is refreshed).
    Task UpsertAsync(PaymentRecord record, CancellationToken ct = default);

    Task<PaymentRecord?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
}
