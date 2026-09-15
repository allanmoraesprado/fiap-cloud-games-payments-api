using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaymentsApi.Contracts;
using PaymentsApi.Messaging;
using PaymentsApi.Payments;
using Xunit;

namespace PaymentsApi.Tests;

public class PaymentProcessorTests
{
    private const string Topic = "fcg.payments.processed";

    private readonly InMemoryPaymentRepository _repository = new();
    private readonly Mock<IEventPublisher> _publisher = new();

    private PaymentProcessor Build(IPaymentRepository? repository = null) => new(
        new PaymentSimulator(Options.Create(new PaymentSettings { RejectAboveAmount = 1000m })),
        repository ?? _repository,
        _publisher.Object,
        Options.Create(new KafkaSettings { PaymentProcessedTopic = Topic }),
        NullLogger<PaymentProcessor>.Instance);

    private static OrderPlacedEvent Order(decimal price) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), price, DateTime.UtcNow.AddSeconds(-5));

    [Fact]
    public async Task Approved_order_is_persisted_and_published()
    {
        var order = Order(49.90m);

        var evt = await Build().ProcessAsync(order);

        evt.Status.Should().Be(PaymentSimulator.Approved);
        evt.OrderId.Should().Be(order.OrderId);
        _repository.Count.Should().Be(1);
        var stored = _repository.Get(order.OrderId)!;
        stored.Status.Should().Be(PaymentSimulator.Approved);
        stored.UserId.Should().Be(order.UserId);
        stored.GameId.Should().Be(order.GameId);
        stored.Price.Should().Be(49.90m);
        stored.OrderPlacedEventId.Should().Be(order.EventId);
        stored.PaymentProcessedEventId.Should().Be(evt.EventId);
        stored.OrderOccurredAt.Should().Be(order.OccurredAt);
        _publisher.Verify(p => p.PublishAsync(Topic, order.OrderId.ToString(),
            It.Is<object>(m => ((PaymentProcessedEvent)m).Status == PaymentSimulator.Approved), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rejected_order_is_persisted_with_a_reason_and_published()
    {
        var order = Order(1500m);

        var evt = await Build().ProcessAsync(order);

        evt.Status.Should().Be(PaymentSimulator.Rejected);
        var stored = _repository.Get(order.OrderId)!;
        stored.Status.Should().Be(PaymentSimulator.Rejected);
        stored.Reason.Should().Contain("limit");
        _publisher.Verify(p => p.PublishAsync(Topic, order.OrderId.ToString(),
            It.Is<object>(m => ((PaymentProcessedEvent)m).Status == PaymentSimulator.Rejected), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Replaying_the_same_event_keeps_a_single_document_per_order()
    {
        var order = Order(49.90m);
        var processor = Build();

        await processor.ProcessAsync(order);
        var createdAt = _repository.Get(order.OrderId)!.CreatedAt;
        await Task.Delay(5);
        await processor.ProcessAsync(order);

        _repository.Count.Should().Be(1);
        var stored = _repository.Get(order.OrderId)!;
        stored.CreatedAt.Should().Be(createdAt);
        stored.UpdatedAt.Should().BeOnOrAfter(createdAt);
        // At-least-once: the event is published again; CatalogAPI is idempotent on its side.
        _publisher.Verify(p => p.PublishAsync(Topic, order.OrderId.ToString(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Persistence_failure_is_logged_and_does_not_block_publishing()
    {
        var failing = new Mock<IPaymentRepository>();
        failing.Setup(r => r.UpsertAsync(It.IsAny<PaymentRecord>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new TimeoutException("mongo down"));
        var order = Order(49.90m);

        var act = () => Build(failing.Object).ProcessAsync(order);

        await act.Should().NotThrowAsync();
        _publisher.Verify(p => p.PublishAsync(Topic, order.OrderId.ToString(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
