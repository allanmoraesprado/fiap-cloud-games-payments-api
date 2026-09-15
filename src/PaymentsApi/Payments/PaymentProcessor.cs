using Microsoft.Extensions.Options;
using PaymentsApi.Contracts;
using PaymentsApi.Messaging;
using PaymentsApi.Observability;

namespace PaymentsApi.Payments;

// Handles one OrderPlacedEvent: decide -> persist the payment history (MongoDB) -> publish
// PaymentProcessedEvent. Kafka behaviour is unchanged from Phase 2; persistence was added in Phase 3.
public sealed class PaymentProcessor
{
    private readonly PaymentSimulator _simulator;
    private readonly IPaymentRepository _payments;
    private readonly IEventPublisher _publisher;
    private readonly KafkaSettings _kafka;
    private readonly ILogger<PaymentProcessor> _logger;

    public PaymentProcessor(
        PaymentSimulator simulator,
        IPaymentRepository payments,
        IEventPublisher publisher,
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<PaymentProcessor> logger)
    {
        _simulator = simulator;
        _payments = payments;
        _publisher = publisher;
        _kafka = kafkaOptions.Value;
        _logger = logger;
    }

    public async Task<PaymentProcessedEvent> ProcessAsync(OrderPlacedEvent order, CancellationToken ct = default)
    {
        var decision = _simulator.Evaluate(order.Price);
        var now = DateTime.UtcNow;
        FcgMetrics.PaymentDecisions.WithLabels(FcgMetrics.StatusLabel(decision.Status)).Inc();

        _logger.LogInformation(
            "Payment {Status} for order {OrderId} (user {UserId}, game {GameId}, price {Price}): {Reason}",
            decision.Status, order.OrderId, order.UserId, order.GameId, order.Price, decision.Reason);

        var evt = new PaymentProcessedEvent(
            Guid.NewGuid(), order.OrderId, order.UserId, order.GameId, order.Price, decision.Status, now);

        var record = new PaymentRecord
        {
            Id = Guid.NewGuid(),
            OrderId = order.OrderId,
            UserId = order.UserId,
            GameId = order.GameId,
            Price = order.Price,
            Status = decision.Status,
            Reason = decision.Reason,
            OrderPlacedEventId = order.EventId,
            PaymentProcessedEventId = evt.EventId,
            OrderOccurredAt = order.OccurredAt,
            ProcessedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            await _payments.UpsertAsync(record, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // No outbox in this MVP: the decision is deterministic and CatalogAPI still needs the
            // event, so a persistence failure is logged and must not block the event flow.
            FcgMetrics.HistoryWrites.WithLabels("failed").Inc();
            _logger.LogError(ex, "Failed to persist payment history for order {OrderId}; publishing the event anyway.", order.OrderId);
        }

        await _publisher.PublishAsync(_kafka.PaymentProcessedTopic, order.OrderId.ToString(), evt, ct);
        return evt;
    }
}
