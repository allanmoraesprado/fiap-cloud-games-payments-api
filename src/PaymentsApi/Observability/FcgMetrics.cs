using Prometheus;

namespace PaymentsApi.Observability;

// Custom Prometheus counters (Phase 3). Exposed on /metrics next to the default HTTP metrics.
// Labels are low-cardinality only: never user ids, order ids, game ids or other PII.
public static class FcgMetrics
{
    public static readonly Counter EventsConsumed = Prometheus.Metrics.CreateCounter(
        "fcg_events_consumed_total",
        "Kafka events consumed by topic and result (processed|malformed).",
        new CounterConfiguration { LabelNames = new[] { "topic", "result" } });

    public static readonly Counter EventsPublished = Prometheus.Metrics.CreateCounter(
        "fcg_events_published_total",
        "Kafka events published by topic and result (success|failure).",
        new CounterConfiguration { LabelNames = new[] { "topic", "result" } });

    public static readonly Counter PaymentDecisions = Prometheus.Metrics.CreateCounter(
        "fcg_payments_decisions_total",
        "Simulated payment decisions by status (approved|rejected).",
        new CounterConfiguration { LabelNames = new[] { "status" } });

    public static readonly Counter HistoryWrites = Prometheus.Metrics.CreateCounter(
        "fcg_payments_history_writes_total",
        "Payment history persistence results (inserted|updated|failed).",
        new CounterConfiguration { LabelNames = new[] { "result" } });

    public static readonly Counter PaymentQueries = Prometheus.Metrics.CreateCounter(
        "fcg_payments_queries_total",
        "Payment status query results (found|not_found|forbidden).",
        new CounterConfiguration { LabelNames = new[] { "result" } });

    static FcgMetrics()
    {
        // Pre-create the known label sets so they are exported as 0 before the first event.
        foreach (var s in new[] { "approved", "rejected" }) PaymentDecisions.WithLabels(s);
        foreach (var r in new[] { "inserted", "updated", "failed" }) HistoryWrites.WithLabels(r);
        foreach (var r in new[] { "found", "not_found", "forbidden" }) PaymentQueries.WithLabels(r);
    }

    // Touching the type runs the static constructor; called once at startup.
    public static void EnsureInitialized() { }

    public static string StatusLabel(string status) => status.ToLowerInvariant();
}
