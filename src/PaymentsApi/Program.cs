using PaymentsApi.Configuration;
using PaymentsApi.Infrastructure.Mongo;
using PaymentsApi.Observability;
using Prometheus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddFcgPayments(builder.Configuration);
builder.Services.AddFcgMongo(builder.Configuration);
builder.Services.AddFcgAuth(builder.Configuration);
builder.Services.AddFcgSwagger();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Ensure the unique orderId index on startup (dev convenience; MongoDB may still be starting).
try { await app.Services.GetRequiredService<MongoPaymentRepository>().EnsureIndexesAsync(); }
catch (Exception ex) { Log.Warning(ex, "MongoDB index creation failed (database may be unavailable)."); }

app.UseSerilogRequestLogging();

// Prometheus (Phase 3): default HTTP request metrics + custom counters on /metrics.
FcgMetrics.EnsureInitialized();
app.UseHttpMetrics();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapMetrics();

app.Run();

public partial class Program { }
