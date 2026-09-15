using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PaymentsApi.Payments;

// One document per OrderId in the MongoDB collection fcg_payments.payments (Phase 3).
// Guids are stored as strings so the collection is readable in mongosh; Price as Decimal128.
public class PaymentRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("orderId")]
    [BsonRepresentation(BsonType.String)]
    public Guid OrderId { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonElement("gameId")]
    [BsonRepresentation(BsonType.String)]
    public Guid GameId { get; set; }

    [BsonElement("price")]
    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Price { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;     // "Approved" | "Rejected"

    [BsonElement("reason")]
    public string Reason { get; set; } = string.Empty;

    [BsonElement("orderPlacedEventId")]
    [BsonRepresentation(BsonType.String)]
    public Guid OrderPlacedEventId { get; set; }

    [BsonElement("paymentProcessedEventId")]
    [BsonRepresentation(BsonType.String)]
    public Guid PaymentProcessedEventId { get; set; }

    [BsonElement("orderOccurredAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime OrderOccurredAt { get; set; }

    [BsonElement("processedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ProcessedAt { get; set; }

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; set; }
}
