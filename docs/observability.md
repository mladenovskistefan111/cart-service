# Observability

| Signal | Implementation | Destination |
|---|---|---|
| **Traces** | OpenTelemetry .NET: ASP.NET Core + HttpClient instrumentation, OTLP/gRPC exporter | Alloy `:4317` → Tempo |
| **Metrics** | OpenTelemetry .NET: ASP.NET Core, Kestrel and runtime instrumentation, `PeriodicExportingMetricReader` over OTLP/gRPC every 15 s (replaces `prometheus-net`) | Alloy → Mimir |
| **Logs** | `ILogger` console to stdout (plain text by default) **and** the OTel logging provider over OTLP/gRPC with trace ids | Alloy → Loki |
| **Profiles** | none | — |

Metric names the dashboard uses: `http_server_request_duration_seconds_{bucket,count,sum}` (labels `http_route` — e.g. `/hipstershop.CartService/AddItem` — `http_response_status_code`, `http_request_method`) and `http_server_active_requests`; plus `kestrel_*`, `aspnetcore_*` and `dotnet_*` runtime metrics. gRPC calls are HTTP/2 requests, so the per-method breakdown comes from `http_route`.

Every series carries `job="cart-service"` (from `service.name`) and `instance=<pod name>` (`service.instance.id`), which is what the Grafana dashboards select on. Logs land in Loki with `service_name="cart-service"` and trace/span ids, so a log line links to its trace.
