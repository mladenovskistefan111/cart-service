using Grpc.Core;
using Grpc.Health.V1;
using cartservice.store;

namespace cartservice.services
{
    public class HealthCheckService(RedisCartStore store, ILogger<HealthCheckService> logger) : Health.HealthBase
    {
        private readonly RedisCartStore _store = store;
        private readonly ILogger<HealthCheckService> _logger = logger;

        public override async Task<HealthCheckResponse> Check(
            HealthCheckRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Health check called");
            bool alive = await _store.PingAsync();
            HealthCheckResponse.Types.ServingStatus status = alive
                ? HealthCheckResponse.Types.ServingStatus.Serving
                : HealthCheckResponse.Types.ServingStatus.NotServing;

            return new HealthCheckResponse { Status = status };
        }
    }
}
