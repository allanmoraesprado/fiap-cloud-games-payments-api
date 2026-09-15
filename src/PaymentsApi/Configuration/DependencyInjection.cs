using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PaymentsApi.Consumers;
using PaymentsApi.Infrastructure.Mongo;
using PaymentsApi.Messaging;
using PaymentsApi.Payments;

namespace PaymentsApi.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddFcgPayments(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<KafkaSettings>(config.GetSection("Kafka"));
        services.Configure<PaymentSettings>(config.GetSection("Payment"));

        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddSingleton<PaymentSimulator>();
        services.AddSingleton<PaymentProcessor>();
        services.AddSingleton<PaymentQueryService>();
        services.AddHostedService<OrderPlacedConsumer>();

        return services;
    }

    // MongoDB (Phase 3): payment history, one document per order in fcg_payments.payments.
    public static IServiceCollection AddFcgMongo(this IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection("Mongo");
        var mongo = section.Get<MongoSettings>() ?? new MongoSettings();
        services.Configure<MongoSettings>(section);

        // Driver 3.x refuses to serialize Guids without an explicit representation; the document
        // stores Guids as strings (readable in mongosh), this is the safety net for any other Guid.
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // One MongoClient per process (thread-safe, pooled). Connection is lazy.
        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongo.ConnectionString));
        services.AddSingleton<MongoPaymentRepository>();
        services.AddSingleton<IPaymentRepository>(sp => sp.GetRequiredService<MongoPaymentRepository>());

        return services;
    }

    // Validates tokens issued by UsersAPI using the SAME shared secret/issuer/audience
    // (defense in depth: Kong already validates the JWT at the edge).
    public static IServiceCollection AddFcgAuth(this IServiceCollection services, IConfiguration config)
    {
        var jwt = config.GetSection("Jwt").Get<JwtSettings>()
                  ?? throw new InvalidOperationException("Missing Jwt settings.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
