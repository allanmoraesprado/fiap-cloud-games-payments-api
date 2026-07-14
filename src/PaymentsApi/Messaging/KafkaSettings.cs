namespace PaymentsApi.Messaging;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = "localhost:29092";
    public string OrderPlacedTopic { get; set; } = "fcg.orders.placed";
    public string PaymentProcessedTopic { get; set; } = "fcg.payments.processed";
    public string ConsumerGroup { get; set; } = "payments-service";
}
