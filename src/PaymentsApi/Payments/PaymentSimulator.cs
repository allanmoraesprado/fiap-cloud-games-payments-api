using System.Globalization;
using Microsoft.Extensions.Options;

namespace PaymentsApi.Payments;

public sealed record PaymentDecision(string Status, string Reason);

// Deterministic, local-only payment simulation. No external providers, SDKs or callbacks.
//   price <= 0                -> Rejected (invalid amount)
//   price > RejectAboveAmount -> Rejected (configurable, default 1000)
//   otherwise                 -> Approved
public class PaymentSimulator
{
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    private readonly decimal _rejectAboveAmount;

    public PaymentSimulator(IOptions<PaymentSettings> options)
        => _rejectAboveAmount = options.Value.RejectAboveAmount;

    // Status plus a human-readable reason (stored in the payment history).
    public PaymentDecision Evaluate(decimal price)
    {
        if (price <= 0)
            return new PaymentDecision(Rejected, "Invalid amount: the price must be greater than zero.");
        if (price > _rejectAboveAmount)
            return new PaymentDecision(Rejected,
                $"Amount above the approval limit ({_rejectAboveAmount.ToString("0.00", CultureInfo.InvariantCulture)}).");
        return new PaymentDecision(Approved, "Approved by the payment simulation.");
    }

    public string Decide(decimal price) => Evaluate(price).Status;
}
