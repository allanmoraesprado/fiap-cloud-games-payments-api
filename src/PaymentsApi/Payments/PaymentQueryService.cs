using PaymentsApi.Observability;

namespace PaymentsApi.Payments;

public sealed record PaymentResponse(
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    string Status,
    string Reason,
    DateTime OrderOccurredAt,
    DateTime ProcessedAt);

public enum PaymentLookupOutcome { Found, NotFound, Forbidden }

public sealed record PaymentLookupResult(PaymentLookupOutcome Outcome, PaymentResponse? Payment)
{
    public static PaymentLookupResult NotFound() => new(PaymentLookupOutcome.NotFound, null);
    public static PaymentLookupResult Forbidden() => new(PaymentLookupOutcome.Forbidden, null);
    public static PaymentLookupResult Found(PaymentResponse payment) => new(PaymentLookupOutcome.Found, payment);
}

// Read side of the payment history. Ownership/role authorization lives here, inside the service,
// even though Kong already validated the token at the edge.
public sealed class PaymentQueryService
{
    private readonly IPaymentRepository _payments;

    public PaymentQueryService(IPaymentRepository payments) => _payments = payments;

    public async Task<PaymentLookupResult> GetByOrderAsync(Guid orderId, Guid callerUserId, bool callerIsAdmin, CancellationToken ct = default)
    {
        var record = await _payments.GetByOrderIdAsync(orderId, ct);
        if (record is null)
        {
            FcgMetrics.PaymentQueries.WithLabels("not_found").Inc();
            return PaymentLookupResult.NotFound();
        }

        // A regular user only sees their own payments; Admin sees any payment.
        if (!callerIsAdmin && record.UserId != callerUserId)
        {
            FcgMetrics.PaymentQueries.WithLabels("forbidden").Inc();
            return PaymentLookupResult.Forbidden();
        }

        FcgMetrics.PaymentQueries.WithLabels("found").Inc();
        return PaymentLookupResult.Found(new PaymentResponse(
            record.OrderId, record.UserId, record.GameId, record.Price,
            record.Status, record.Reason, record.OrderOccurredAt, record.ProcessedAt));
    }
}
