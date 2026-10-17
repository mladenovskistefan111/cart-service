# Configuration

| Variable | Required | Default | Description |
|---|---|---|---|
| `REDIS_ADDR` | **Yes** | — | StackExchange.Redis configuration string, e.g. `cart-redis:6379`. Process aborts at startup if unset. Extra options go after commas: `cart-redis:6379,password=<secret>,ssl=false` |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` (in container) | Standard ASP.NET Core |
| `Kestrel__Endpoints__Grpc__Url` | No | `http://0.0.0.0:7070` | Override the gRPC listener (standard .NET env-var-to-config mapping) |
| `Logging__Console__FormatterName` | No | default (plain text) | Set to `json` for JSON console logs |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | No | `http://localhost:4317` | OTLP/**gRPC** endpoint for **traces, metrics and logs** (scheme required), e.g. `http://alloy.monitoring.svc.cluster.local:4317`. Legacy fallback: `COLLECTOR_SERVICE_ADDR` (`host:port`) |
| `OTEL_METRIC_EXPORT_INTERVAL` | No | `15000` | Milliseconds between metric pushes |
| `OTEL_SERVICE_NAME` | No | `cart-service` | `service.name` — becomes the Prometheus `job` and the Loki `service_name`; keep the default, dashboards depend on it |
| `SERVICE_VERSION` | No | `1.0.0` | `service.version` resource attribute |

Notes:

- Ports are defined in [`src/appsettings.json`](https://github.com/platform-boutique/app-cart-service/blob/main/src/appsettings.json): Kestrel `Grpc` = `Http2` on 7070. The image also sets `ASPNETCORE_HTTP_PORTS=7070`, but explicit Kestrel endpoints take precedence. There is no plain `PORT` variable.
- There is **no profiling** (no Pyroscope integration).
- Redis is connected to lazily on first use, so the process starts even if Redis is down — it just reports `NOT_SERVING` and fails RPCs until Redis is reachable.
- Telemetry is **pushed**, not scraped: traces, metrics and logs share one OTLP/gRPC endpoint (Alloy); profiles go to Pyroscope (via Alloy). `service.instance.id` is the pod hostname, so replicas stay distinct in Mimir.
- The former Prometheus endpoint (`METRICS_PORT`) no longer exists. Logs keep going to stdout and are additionally exported over OTLP with trace ids.
