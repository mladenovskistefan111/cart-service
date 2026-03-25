using cartservice.services;
using cartservice.store;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// --- Redis ---
var redisAddr = Environment.GetEnvironmentVariable("REDIS_ADDR")
    ?? throw new InvalidOperationException("REDIS_ADDR environment variable is not set.");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisAddr;
});
builder.Services.AddSingleton<RedisCartStore>();

// --- gRPC ---
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

// --- Tracing + Runtime Metrics via OTEL ---
var collectorAddr = Environment.GetEnvironmentVariable("COLLECTOR_SERVICE_ADDR");
var serviceName   = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "cart-service";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrEmpty(collectorAddr))
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri($"http://{collectorAddr}"));
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation();
    });

// --- App ---
var app = builder.Build();

app.UseRouting();
app.UseHttpMetrics();
app.UseGrpcMetrics();
app.MapGrpcService<CartService>();
app.MapGrpcService<HealthCheckService>();
app.MapGrpcReflectionService();
app.MapMetrics("/metrics");
app.MapGet("/", () => "cart-service gRPC — use a gRPC client to interact.");

app.Run();