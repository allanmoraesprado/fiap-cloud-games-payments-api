namespace PaymentsApi.Contracts;

public record OrderPlacedEvent(
    Guid EventId,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    DateTime OccurredAt);
