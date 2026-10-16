using cartservice.services;
using cartservice.store;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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

// --- Traces, metrics and logs via OpenTelemetry, pushed over OTLP/gRPC (Alloy -> Tempo/Mimir/Loki) ---
// OTEL_EXPORTER_OTLP_ENDPOINT is the standard variable (scheme required, e.g. http://alloy:4317);
// COLLECTOR_SERVICE_ADDR (host:port) is kept as a legacy fallback.
string otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? (Environment.GetEnvironmentVariable("COLLECTOR_SERVICE_ADDR") is { Length: > 0 } legacyAddr
        ? $"http://{legacyAddr}"
        : "http://localhost:4317");
string serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "cart-service";
string serviceVersion = Environment.GetEnvironmentVariable("SERVICE_VERSION") ?? "1.0.0";
// Distinguishes replicas: in Kubernetes the hostname is the pod name.
string serviceInstanceId = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
int metricIntervalMs = int.TryParse(Environment.GetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL"), out int ms) ? ms : 15000;

void ConfigureOtlp(OtlpExporterOptions o)
{
    o.Endpoint = new Uri(otlpEndpoint);
    o.Protocol = OtlpExportProtocol.Grpc;
}

_ = builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName, serviceVersion: serviceVersion, serviceInstanceId: serviceInstanceId))
    .WithTracing(tracing =>
    {
        _ = tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(ConfigureOtlp);
    })
    .WithMetrics(metrics =>
    {
        _ = metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("Grpc.AspNetCore.Server") // built-in gRPC server call metrics
            .AddOtlpExporter((o, reader) =>
            {
                ConfigureOtlp(o);
                reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = metricIntervalMs;
            });
    })
    .WithLogging(logging =>
    {
        _ = logging.AddOtlpExporter(ConfigureOtlp);
    });

// --- App ---
WebApplication app = builder.Build();

app.UseRouting();
app.MapGrpcService<CartService>();
app.MapGrpcService<HealthCheckService>();
app.MapGrpcReflectionService();
_ = app.MapGet("/", () => "cart-service gRPC — use a gRPC client to interact.");

app.Run();
