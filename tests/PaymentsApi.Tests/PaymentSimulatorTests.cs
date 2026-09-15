using PaymentsApi.Payments;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace PaymentsApi.Tests;

public class PaymentSimulatorTests
{
    private static PaymentSimulator Build(decimal rejectAbove = 1000m)
        => new(Options.Create(new PaymentSettings { RejectAboveAmount = rejectAbove }));

    [Fact]
    public void Approves_typical_price()
        => Build().Decide(49.90m).Should().Be(PaymentSimulator.Approved);

    [Fact]
    public void Approves_at_threshold_boundary()
        => Build(1000m).Decide(1000m).Should().Be(PaymentSimulator.Approved);

    [Fact]
    public void Rejects_zero_price()
        => Build().Decide(0m).Should().Be(PaymentSimulator.Rejected);

    [Fact]
    public void Rejects_negative_price()
        => Build().Decide(-5m).Should().Be(PaymentSimulator.Rejected);

    [Fact]
    public void Rejects_price_above_threshold()
        => Build(1000m).Decide(1500m).Should().Be(PaymentSimulator.Rejected);

    [Fact]
    public void Evaluate_explains_a_rejection_above_the_limit()
    {
        var decision = Build(1000m).Evaluate(1500m);

        decision.Status.Should().Be(PaymentSimulator.Rejected);
        decision.Reason.Should().Contain("limit").And.Contain("1000.00");
    }

    [Fact]
    public void Evaluate_explains_an_approval()
    {
        var decision = Build().Evaluate(49.90m);

        decision.Status.Should().Be(PaymentSimulator.Approved);
        decision.Reason.Should().Contain("Approved");
    }
}
