namespace PaymentsApi.Payments;

public class PaymentSettings
{
    // Orders priced above this amount are Rejected by the simulation. Default 1000.
    public decimal RejectAboveAmount { get; set; } = 1000m;
}
