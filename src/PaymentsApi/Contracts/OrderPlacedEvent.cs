namespace PaymentsApi.Contracts;

// Canonical reference: orchestration/contracts/README.md (mirrored copy). Consumed.
public record OrderPlacedEvent(
    Guid EventId,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    DateTime OccurredAt);
