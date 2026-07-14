namespace PaymentsApi.Contracts;

// Canonical reference: orchestration/contracts/README.md (mirrored copy). Produced.
public record PaymentProcessedEvent(
    Guid EventId,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    string Status,        // "Approved" | "Rejected"
    DateTime OccurredAt);
