# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /app

COPY src/cartservice.csproj ./src/
RUN dotnet restore src/cartservice.csproj

COPY src/ ./src/
RUN dotnet publish src/cartservice.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# --- Stage 2: Run ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

COPY --from=builder /app/publish .

ENV ASPNETCORE_HTTP_PORTS=7070 \
    DOTNET_EnableDiagnostics=0

EXPOSE 7070 9090

USER app

ENTRYPOINT ["./cartservice"]