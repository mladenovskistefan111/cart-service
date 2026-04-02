using cartservice.services;
using cartservice.store;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// --- Redis ---
string redisAddr = Environment.GetEnvironmentVariable("REDIS_ADDR")
    ?? throw new InvalidOperationException("REDIS_ADDR environment variable is not set.");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisAddr;
});

// Register both the concrete type (for Redis health-check) and the abstraction (for CartService).
builder.Services.AddSingleton<RedisCartStore>();
builder.Services.AddSingleton<ICartStore>(sp => sp.GetRequiredService<RedisCartStore>());

// --- gRPC ---
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

// --- Tracing + Runtime Metrics via OTEL ---
string? collectorAddr = Environment.GetEnvironmentVariable("COLLECTOR_SERVICE_ADDR");
string serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "cart-service";

_ = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithTracing(tracing =>
    {
        _ = tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrEmpty(collectorAddr))
        {
            _ = tracing.AddOtlpExporter(o => o.Endpoint = new Uri($"http://{collectorAddr}"));
        }
    })
    .WithMetrics(metrics =>
    {
        _ = metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation();
    });

// --- App ---
WebApplication app = builder.Build();

app.UseRouting();
app.UseHttpMetrics();
app.UseGrpcMetrics();
app.MapGrpcService<CartService>();
app.MapGrpcService<HealthCheckService>();
app.MapGrpcReflectionService();
app.MapMetrics("/metrics");
_ = app.MapGet("/", () => "cart-service gRPC — use a gRPC client to interact.");

app.Run();
