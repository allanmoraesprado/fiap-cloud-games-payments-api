using System.Text.Json;
using Confluent.Kafka;
using PaymentsApi.Contracts;
using PaymentsApi.Messaging;
using PaymentsApi.Payments;
using Microsoft.Extensions.Options;

namespace PaymentsApi.Consumers;

// Consumes fcg.orders.placed, simulates the payment, and publishes PaymentProcessedEvent.
public class OrderPlacedConsumer : BackgroundService
{
    private readonly KafkaSettings _settings;
    private readonly IEventPublisher _publisher;
    private readonly PaymentSimulator _simulator;
    private readonly ILogger<OrderPlacedConsumer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OrderPlacedConsumer(
        IOptions<KafkaSettings> options,
        IEventPublisher publisher,
        PaymentSimulator simulator,
        ILogger<OrderPlacedConsumer> logger)
    {
        _settings = options.Value;
        _publisher = publisher;
        _simulator = simulator;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

    private async Task ConsumeLoop(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_settings.OrderPlacedTopic);
        _logger.LogInformation("Subscribed to {Topic} as group {Group}", _settings.OrderPlacedTopic, _settings.ConsumerGroup);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string> cr;
                try { cr = consumer.Consume(stoppingToken); }
                catch (ConsumeException ex) { _logger.LogWarning(ex, "Consume error; retrying."); continue; }

                if (cr?.Message?.Value is null) continue;

                try
                {
                    var order = JsonSerializer.Deserialize<OrderPlacedEvent>(cr.Message.Value, JsonOptions);
                    if (order is not null) await ProcessAsync(order, stoppingToken);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Malformed OrderPlacedEvent; skipping message.");
                }

                // Commit after handling (publish-then-commit -> at-least-once).
                try { consumer.Commit(cr); }
                catch (KafkaException ex) { _logger.LogWarning(ex, "Commit failed."); }
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task ProcessAsync(OrderPlacedEvent order, CancellationToken ct)
    {
        var status = _simulator.Decide(order.Price);
        _logger.LogInformation(
            "Payment {Status} for order {OrderId} (user {UserId}, game {GameId}, price {Price})",
            status, order.OrderId, order.UserId, order.GameId, order.Price);

        var evt = new PaymentProcessedEvent(
            Guid.NewGuid(), order.OrderId, order.UserId, order.GameId, order.Price, status, DateTime.UtcNow);
        await _publisher.PublishAsync(_settings.PaymentProcessedTopic, order.OrderId.ToString(), evt, ct);
    }
}
