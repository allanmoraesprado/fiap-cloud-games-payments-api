namespace PaymentsApi.Contracts;

public record PaymentProcessedEvent(
    Guid EventId,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    string Status,        // "Approved" | "Rejected"
    DateTime OccurredAt);
