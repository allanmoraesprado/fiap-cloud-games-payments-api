using FluentAssertions;
using PaymentsApi.Payments;
using Xunit;

namespace PaymentsApi.Tests;

public class PaymentQueryServiceTests
{
    private readonly InMemoryPaymentRepository _repository = new();
    private readonly PaymentQueryService _sut;

    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid OtherUser = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    public PaymentQueryServiceTests() => _sut = new PaymentQueryService(_repository);

    private async Task<PaymentRecord> Seed(string status = "Approved")
    {
        var now = DateTime.UtcNow;
        var record = new PaymentRecord
        {
            Id = Guid.NewGuid(), OrderId = Guid.NewGuid(), UserId = Owner, GameId = Guid.NewGuid(),
            Price = 99.90m, Status = status, Reason = "test", OrderPlacedEventId = Guid.NewGuid(),
            PaymentProcessedEventId = Guid.NewGuid(), OrderOccurredAt = now, ProcessedAt = now, CreatedAt = now, UpdatedAt = now
        };
        await _repository.UpsertAsync(record);
        return record;
    }

    [Fact]
    public async Task Returns_NotFound_when_the_order_has_no_payment()
    {
        var result = await _sut.GetByOrderAsync(Guid.NewGuid(), Owner, callerIsAdmin: false);

        result.Outcome.Should().Be(PaymentLookupOutcome.NotFound);
        result.Payment.Should().BeNull();
    }

    [Fact]
    public async Task Owner_can_read_their_own_payment()
    {
        var record = await Seed();

        var result = await _sut.GetByOrderAsync(record.OrderId, Owner, callerIsAdmin: false);

        result.Outcome.Should().Be(PaymentLookupOutcome.Found);
        result.Payment.Should().NotBeNull();
        result.Payment!.OrderId.Should().Be(record.OrderId);
        result.Payment.UserId.Should().Be(Owner);
        result.Payment.Status.Should().Be("Approved");
        result.Payment.Price.Should().Be(99.90m);
    }

    [Fact]
    public async Task Another_regular_user_is_forbidden()
    {
        var record = await Seed();

        var result = await _sut.GetByOrderAsync(record.OrderId, OtherUser, callerIsAdmin: false);

        result.Outcome.Should().Be(PaymentLookupOutcome.Forbidden);
        result.Payment.Should().BeNull();
    }

    [Fact]
    public async Task Admin_can_read_any_payment()
    {
        var record = await Seed("Rejected");

        var result = await _sut.GetByOrderAsync(record.OrderId, Admin, callerIsAdmin: true);

        result.Outcome.Should().Be(PaymentLookupOutcome.Found);
        result.Payment!.Status.Should().Be("Rejected");
    }
}
