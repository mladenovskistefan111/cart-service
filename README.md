# cart-service

A gRPC service that manages shopping carts for the platform-demo e-commerce platform. Carts are stored in Redis keyed by user (session) id. The service itself is stateless, so it can be scaled horizontally; all state lives in `cart-redis`.

Part of the [platform-demo](https://github.com/mladenovskistefan111) platform — microservices, observability (LGTM stack), GitOps (Argo CD), policy enforcement (Kyverno), infrastructure provisioning (Crossplane) and an internal developer portal (Backstage). See the master `README.md` in the platform-demo root for the whole-system view.

## At a glance

| | |
|---|---|
| **Language / runtime** | C# / ASP.NET Core on .NET 10, gRPC (`Grpc.AspNetCore`) |
| **Image** | `mladenovskistefan/cart-service` |
| **Protocol / port** | gRPC (HTTP/2 cleartext) on `7070` |
| **Metrics** | Pushed over OTLP/gRPC (OpenTelemetry .NET) → Alloy → Mimir; no scrape endpoint |
| **Health** | Standard `grpc.health.v1.Health/Check` on `7070` — **reports `NOT_SERVING` when Redis is unreachable** |
| **State** | Redis (`cart-redis:6379`). Service is stateless |
| **Depends on** | Redis (hard runtime dependency); optional OTLP collector |
| **Called by** | `frontend` (all cart operations), `checkout-service` (`GetCart`, `EmptyCart`) |
| **gRPC reflection** | Enabled — `grpcurl` works without `-proto` |

## API

Package `hipstershop`, service `CartService` ([`src/protos/cart.proto`](src/protos/cart.proto)).

| Method | Request | Response | Behaviour |
|---|---|---|---|
| `AddItem` | `AddItemRequest{ user_id, CartItem{ product_id, quantity } }` | `Empty` | Creates the cart if absent; otherwise adds `quantity` to an existing line or appends a new one |
| `GetCart` | `GetCartRequest{ user_id }` | `Cart{ user_id, repeated CartItem items }` | Returns the cart, or an empty cart if none exists |
| `EmptyCart` | `EmptyCartRequest{ user_id }` | `Empty` | Overwrites the cart with an empty one |

Errors: any Redis failure is returned as gRPC `FAILED_PRECONDITION` ("Can't access cart storage. …").

### Storage model

- Redis key = `user_id` (raw, no prefix — the `IDistributedCache` instance has no `InstanceName`).
- Value = protobuf-serialised `Cart` (binary).
- **No TTL.** Carts live until Redis is wiped. `EmptyCart` writes an empty cart rather than deleting the key.
- `AddItem` is read-modify-write and not atomic: two concurrent adds for the same user can lose an update.

## Configuration

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

- Ports are defined in [`src/appsettings.json`](src/appsettings.json): Kestrel `Grpc` = `Http2` on 7070. The image also sets `ASPNETCORE_HTTP_PORTS=7070`, but explicit Kestrel endpoints take precedence. There is no plain `PORT` variable.
- There is **no profiling** (no Pyroscope integration).
- Redis is connected to lazily on first use, so the process starts even if Redis is down — it just reports `NOT_SERVING` and fails RPCs until Redis is reachable.
- Telemetry is **pushed**, not scraped: traces, metrics and logs share one OTLP/gRPC endpoint (Alloy); profiles go to Pyroscope (via Alloy). `service.instance.id` is the pod hostname, so replicas stay distinct in Mimir.
- The former Prometheus endpoint (`METRICS_PORT`) no longer exists. Logs keep going to stdout and are additionally exported over OTLP with trace ids.

## Container image

Multi-stage [`Dockerfile`](Dockerfile):

| Stage | Base | What happens |
|---|---|---|
| builder | `mcr.microsoft.com/dotnet/sdk:10.0` | `dotnet restore` (csproj only, for layer caching), then `dotnet publish -c Release` |
| runtime | `mcr.microsoft.com/dotnet/aspnet:10.0` | copies publish output, runs as `app` |

- **Entrypoint:** `./cartservice` (working dir `/app`).
- **User:** `app` — **UID/GID `1654`** (verified on the built image; set `runAsUser: 1654` for `runAsNonRoot`).
- **ENV:** `ASPNETCORE_HTTP_PORTS=7070`, `DOTNET_EnableDiagnostics=0` (disables the diagnostics IPC socket, so no writable `/tmp` is needed for that).
- **EXPOSE:** `7070`. **No `HEALTHCHECK`**, and the runtime image has no `curl`/`wget`, so exec probes are not an option — use `grpc:` or `tcpSocket`.
- **Image size:** ~97 MB.
- **Build context:** a root-level `.dockerignore` (added) excludes `bin/`, `obj/`, `tests/`, `.git`. The previous `src/.dockerignore` had no effect, and a host-built `src/obj` made the in-image `dotnet publish` fail with `NETSDK1064`.

## Kubernetes deployment notes

**Workload:** stateless `Deployment`; scale freely. Needs a reachable Redis (the existing `cart-redis` chart / `redis:7-alpine`, port `6379`).

**Ports to declare**

| Name | Port | Protocol | Expose via Service? |
|---|---|---|---|
| `grpc` | `7070` | TCP (HTTP/2) | Yes — callers dial `cart-service:7070` |

**Service DNS name expected by callers:** `cart-service` (`frontend`: `CART_SERVICE_ADDR=cart-service:7070`, `checkout-service`: `CART_SERVICE_ADDR=cart-service:7070`). Use FQDNs if callers are in other namespaces.

**Probes** — the gRPC health check pings Redis, which is exactly what you want for *readiness* but **not** for liveness (restarting cart pods does not fix a Redis outage and would cause a restart storm):

```yaml
startupProbe:
  tcpSocket: { port: 7070 }
  periodSeconds: 2
  failureThreshold: 30
readinessProbe:            # Redis-aware: pod leaves the endpoints while Redis is down
  grpc: { port: 7070 }
  periodSeconds: 5
  timeoutSeconds: 6        # Redis ping can take up to ~5 s to time out
livenessProbe:             # process-only
  tcpSocket: { port: 7070 }
  periodSeconds: 10
  failureThreshold: 3
```

**Security context**

```yaml
securityContext:
  runAsNonRoot: true
  runAsUser: 1654
  runAsGroup: 1654
  allowPrivilegeEscalation: false
  capabilities: { drop: ["ALL"] }
  readOnlyRootFilesystem: true   # verified in-cluster; no writable /tmp was needed
```

**Resources** — requests sized from measured idle usage in the test stack (see the chart's `values.yaml` for limits):

```yaml
resources:
  requests: { cpu: 100m, memory: 64Mi }
  limits:   { cpu: "1", memory: 256Mi }
```

**Config / secrets:** `REDIS_ADDR` is not secret while Redis has no password. If you add auth, put the *whole* `REDIS_ADDR` string (it embeds `password=…`) in a `Secret` and use `secretKeyRef`.

**Telemetry:** pushed, not scraped — OTLP/gRPC to Alloy (`global.monitoring.otlpEndpoint`); no profiler. Metrics carry `job="cart-service"` and `instance=<pod>`; no scrape annotations or `ServiceMonitor` are needed.

**Helm chart:** [`platform-demo-helm/cart-service`](../platform-demo-helm/cart-service) (built on the shared `platform-common` library chart). It ships with HA defaults — 2 replicas,
HPA, PodDisruptionBudget, topology spread, NetworkPolicy, restricted pod security context, probes and the full env contract (see Configuration) —
and was verified on a 3-node kind cluster under the `restricted` Pod Security Standard.

## Observability

| Signal | Implementation | Destination |
|---|---|---|
| **Traces** | OpenTelemetry .NET: ASP.NET Core + HttpClient instrumentation, OTLP/gRPC exporter | Alloy `:4317` → Tempo |
| **Metrics** | OpenTelemetry .NET: ASP.NET Core, Kestrel and runtime instrumentation, `PeriodicExportingMetricReader` over OTLP/gRPC every 15 s (replaces `prometheus-net`) | Alloy → Mimir |
| **Logs** | `ILogger` console to stdout (plain text by default) **and** the OTel logging provider over OTLP/gRPC with trace ids | Alloy → Loki |
| **Profiles** | none | — |

Metric names the dashboard uses: `http_server_request_duration_seconds_{bucket,count,sum}` (labels `http_route` — e.g. `/hipstershop.CartService/AddItem` — `http_response_status_code`, `http_request_method`) and `http_server_active_requests`; plus `kestrel_*`, `aspnetcore_*` and `dotnet_*` runtime metrics. gRPC calls are HTTP/2 requests, so the per-method breakdown comes from `http_route`.

Every series carries `job="cart-service"` (from `service.name`) and `instance=<pod name>` (`service.instance.id`), which is what the Grafana dashboards select on. Logs land in Loki with `service_name="cart-service"` and trace/span ids, so a log line links to its trace.

## Running locally

Requirements: .NET 10 SDK, Docker, a Redis, [`grpcurl`](https://github.com/fullstorydev/grpcurl).

```bash
# 1. Redis
docker run -d --name cart-redis -p 6379:6379 redis:7-alpine

# 2. run from source
cd src && REDIS_ADDR=localhost:6379 dotnet run

# or via Docker
docker build -t cart-service:local .
docker run --rm --network host -e REDIS_ADDR=localhost:6379 cart-service:local

# tests
dotnet test tests/cartservice.tests.csproj
```

### Manual gRPC testing (reflection is on)

```bash
grpcurl -plaintext -d '{"user_id":"u1","item":{"product_id":"OLJCESPC7Z","quantity":2}}' \
  localhost:7070 hipstershop.CartService/AddItem

grpcurl -plaintext -d '{"user_id":"u1"}' localhost:7070 hipstershop.CartService/GetCart
grpcurl -plaintext -d '{"user_id":"u1"}' localhost:7070 hipstershop.CartService/EmptyCart

grpcurl -plaintext localhost:7070 grpc.health.v1.Health/Check   # NOT_SERVING if Redis is down
```

## Project structure

```
├── src/
│   ├── Program.cs                 # DI, Redis cache, gRPC + reflection, OTel (traces/metrics/logs), endpoints
│   ├── appsettings.json           # Kestrel endpoints (7070 h2, 9090 h1) + log levels
│   ├── cartservice.csproj         # net10.0, TreatWarningsAsErrors, package refs
│   ├── protos/cart.proto          # service contract
│   ├── services/
│   │   ├── CartService.cs         # gRPC handlers
│   │   └── HealthCheckService.cs  # grpc.health.v1 implementation (pings Redis)
│   └── store/
│       ├── ICartStore.cs
│       └── RedisCartStore.cs      # IDistributedCache-backed store
├── Dockerfile                     # sdk:10.0 builder -> aspnet:10.0 runtime
├── cartservice.sln
├── global.json / .editorconfig
└── tests/                         # (not covered here)
```

## Known limitations

- Carts never expire (no TTL) and `AddItem` is not atomic.
- Health is Redis-coupled; see probe guidance above.
- Default console logs are not JSON, so Loki sees unstructured lines unless the formatter is switched.
