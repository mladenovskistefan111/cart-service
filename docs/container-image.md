# Container image

Multi-stage [`Dockerfile`](https://github.com/platform-boutique/app-cart-service/tree/main/Dockerfile):

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
