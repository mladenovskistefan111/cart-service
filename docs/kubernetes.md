# Kubernetes

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

**Helm chart:** [`platform-demo-helm/cart-service`](https://github.com/platform-boutique/platform-demo-helm/tree/main/cart-service) (built on the shared `platform-common` library chart). It ships with HA defaults — 2 replicas,
HPA, PodDisruptionBudget, topology spread, NetworkPolicy, restricted pod security context, probes and the full env contract (see Configuration) —
and was verified under the `restricted` Pod Security Standard on the earlier local 3-node kind cluster (since retired). The platform now targets AWS EKS, where this chart has not been deployed or verified yet.

**Deployment and admission policies:** Argo CD deploys the chart from `platform-demo-helm` (`envs/<env>/values/cart-service.yaml` pins the image digest; promotion is a pull request). In the cluster Kyverno enforces, for this service: pods named `app.kubernetes.io/name: cart-service` that run under the ServiceAccount of the same name (Cilium derives the network identity from that label), an image from this repository pinned by digest and signed by our release pipeline, a read-only root filesystem, resource requests and a memory limit, readiness and liveness probes and no API token. The Helm CI evaluates the rendered chart against these policies before merge; see [`platform-demo-helm/policies`](https://github.com/platform-boutique/platform-demo-helm/tree/main/policies).
