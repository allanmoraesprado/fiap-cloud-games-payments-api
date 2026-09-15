using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using PaymentsApi.Observability;

namespace PaymentsApi.Messaging;

public class KafkaEventPublisher : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IOptions<KafkaSettings> options, ILogger<KafkaEventPublisher> logger)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            MessageTimeoutMs = 5000,
            Acks = Acks.All
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
        _logger = logger;
    }

    public async Task PublishAsync(string topic, string key, object message, CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            var result = await _producer.ProduceAsync(
                topic, new Message<string, string> { Key = key, Value = json }, ct);
            FcgMetrics.EventsPublished.WithLabels(topic, "success").Inc();
            _logger.LogInformation(
                "Published event to {Topic} partition {Partition} offset {Offset}",
                topic, result.Partition.Value, result.Offset.Value);
        }
        catch (Exception ex)
        {
            FcgMetrics.EventsPublished.WithLabels(topic, "failure").Inc();
            _logger.LogWarning(ex, "Failed to publish event to {Topic}; continuing.", topic);
        }
    }

    public void Dispose()
    {
        try { _producer.Flush(TimeSpan.FromSeconds(5)); } catch { /* best-effort drain */ }
        _producer.Dispose();
    }
}
