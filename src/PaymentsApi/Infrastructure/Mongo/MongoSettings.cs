namespace PaymentsApi.Infrastructure.Mongo;

// Bound from the "Mongo" configuration section (Mongo__* environment variables).
// Local/dev placeholders only; the real connection string never lives in the repository.
public class MongoSettings
{
    public string ConnectionString { get; set; } = "mongodb://fcg:fcg@localhost:27017/?authSource=admin";
    public string DatabaseName { get; set; } = "fcg_payments";
    public string PaymentsCollectionName { get; set; } = "payments";
}
