# Develop & test

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
