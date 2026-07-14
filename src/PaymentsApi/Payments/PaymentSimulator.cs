using Microsoft.Extensions.Options;

namespace PaymentsApi.Payments;

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

    public string Decide(decimal price)
    {
        if (price <= 0) return Rejected;
        if (price > _rejectAboveAmount) return Rejected;
        return Approved;
    }
}
