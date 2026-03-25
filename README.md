# cart-service

A gRPC service that manages the shopping cart for the platform-demo e-commerce platform. It stores and retrieves cart state in Redis and is part of a broader microservices platform built with full observability, GitOps, and internal developer platform tooling.

## Overview

The service exposes three gRPC methods:

| Method | Description |
|---|---|
| `AddItem` | Adds an item to a user's cart, or increments quantity if the product already exists |
| `GetCart` | Returns the current cart for a user, or an empty cart if none exists |
| `EmptyCart` | Clears all items from a user's cart |

**Port:** `7070` (gRPC)  
**Metrics Port:** `9090` (Prometheus)  
**Protocol:** gRPC  
**Language:** C# / .NET 10  
**Storage:** Redis (via `cart-redis`)

## Requirements

- .NET 10 SDK
- Docker
- A running Redis instance
- `grpcurl` for manual testing

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `REDIS_ADDR` | Yes | Redis connection string e.g. `localhost:6379` |
| `COLLECTOR_SERVICE_ADDR` | No | OTel collector address e.g. `alloy:4317` (required for tracing) |
| `OTEL_SERVICE_NAME` | No | Service name reported to OTel (default: `cart-service`) |

## Running Locally

### 1. Start Redis

```bash
docker run -d -p 6379:6379 redis:7-alpine
```

### 2. Build and run the service

```bash
cd src
REDIS_ADDR=localhost:6379 dotnet run
```

### 3. Run with Docker

```bash
docker build -t cart-service:local .

docker run -p 7070:7070 -p 9090:9090 \
  -e REDIS_ADDR=localhost:6379 \
  cart-service:local
```

## Testing

### Manual gRPC testing

Install `grpcurl` then:

```bash
# add an item to a cart
grpcurl -plaintext -d '{
  "user_id": "test-user",
  "item": {"product_id": "OLJCESPC7Z", "quantity": 2}
}' localhost:7070 hipstershop.CartService/AddItem

# get a cart
grpcurl -plaintext -d '{"user_id": "test-user"}' \
  localhost:7070 hipstershop.CartService/GetCart

# empty a cart
grpcurl -plaintext -d '{"user_id": "test-user"}' \
  localhost:7070 hipstershop.CartService/EmptyCart

# health check
grpcurl -plaintext localhost:7070 grpc.health.v1.Health/Check

# prometheus metrics
curl localhost:9090/metrics
```

### Load testing

```bash
for user in user-1 user-2 user-3 user-4 user-5; do
  for product in OLJCESPC7Z 66VCHSJNUP 1YMWWN1N4O L9ECAV7KIM 2ZYFJ3GM2N; do
    grpcurl -plaintext -d "{
      \"user_id\": \"$user\",
      \"item\": {\"product_id\": \"$product\", \"quantity\": 1}
    }" localhost:7070 hipstershop.CartService/AddItem
  done
done
```

## Project Structure

```
├── src/
│   ├── Program.cs               # Entry point — server init, DI, tracing, metrics
│   ├── appsettings.json         # Kestrel configuration (HTTP2 on 7070, HTTP1 on 9090)
│   ├── cartservice.csproj       # Project file and package references
│   ├── store/
│   │   └── RedisCartStore.cs    # Redis-backed cart storage
│   ├── services/
│   │   ├── CartService.cs       # gRPC handler — AddItem, GetCart, EmptyCart
│   │   └── HealthCheckService.cs  # gRPC health check with live Redis ping
│   └── protos/
│       └── cart.proto           # Proto definition and gRPC service contract
├── tests/
│   ├── CartServiceTests.cs      # Integration tests
│   └── cartservice.tests.csproj
├── Dockerfile
└── cartservice.sln
```

## Observability

The service is instrumented across traces, metrics, and logs:

| Signal | Implementation |
|---|---|
| **Traces** | OpenTelemetry — exported to Alloy via OTLP gRPC, stored in Tempo |
| **Metrics** | `prometheus-net` — gRPC request counts and HTTP latency on `/metrics`, scraped by Alloy, stored in Mimir |
| **Logs** | `Microsoft.Extensions.Logging` JSON — collected by Alloy from Docker, stored in Loki |

## Documentation

See [`docs/`](./docs) for:

- Service contract and proto definition
- Architecture decision records
- Observability (metrics, traces, logs)
- Runbook

## Part Of

This service is part of [platform-demo](https://github.com/mladenovskistefan111) — a full platform engineering project featuring microservices, observability (LGTM stack), GitOps (Argo CD), policy enforcement (Kyverno), infrastructure provisioning (Crossplane), and an internal developer portal (Backstage).