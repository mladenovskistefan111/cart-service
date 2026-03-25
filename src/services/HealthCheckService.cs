using Grpc.Core;
using Grpc.Health.V1;
using cartservice.store;

namespace cartservice.services;

public class HealthCheckService : Health.HealthBase
{
    private readonly RedisCartStore _store;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(RedisCartStore store, ILogger<HealthCheckService> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public override async Task<HealthCheckResponse> Check(
        HealthCheckRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Health check called");
        var alive = await _store.PingAsync();
        var status = alive
            ? HealthCheckResponse.Types.ServingStatus.Serving
            : HealthCheckResponse.Types.ServingStatus.NotServing;

        return new HealthCheckResponse { Status = status };
    }
}