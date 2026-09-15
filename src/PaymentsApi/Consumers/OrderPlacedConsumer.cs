using System.Text.Json;
using Confluent.Kafka;
using PaymentsApi.Contracts;
using PaymentsApi.Messaging;
using PaymentsApi.Observability;
using PaymentsApi.Payments;
using Microsoft.Extensions.Options;

namespace PaymentsApi.Consumers;

public class OrderPlacedConsumer : BackgroundService
{
    private readonly KafkaSettings _settings;
    private readonly PaymentProcessor _processor;
    private readonly ILogger<OrderPlacedConsumer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public OrderPlacedConsumer(
        IOptions<KafkaSettings> options,
        PaymentProcessor processor,
        ILogger<OrderPlacedConsumer> logger)
    {
        _settings = options.Value;
        _processor = processor;
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
                    // Decide -> persist history (MongoDB, idempotent per OrderId) -> publish.
                    if (order is not null)
                    {
                        await _processor.ProcessAsync(order, stoppingToken);
                        FcgMetrics.EventsConsumed.WithLabels(_settings.OrderPlacedTopic, "processed").Inc();
                    }
                }
                catch (JsonException ex)
                {
                    FcgMetrics.EventsConsumed.WithLabels(_settings.OrderPlacedTopic, "malformed").Inc();
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
}
