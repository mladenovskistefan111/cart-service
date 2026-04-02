using Grpc.Core;

namespace cartservice.tests
{
    /// <summary>
    /// Minimal <see cref="ServerCallContext"/> stub sufficient for unit-testing
    /// gRPC service methods that receive a context but do not use it.
    /// </summary>
    internal sealed class TestServerCallContext : ServerCallContext
    {
        private TestServerCallContext() { }

        public static ServerCallContext Create() => new TestServerCallContext();

        protected override string MethodCore => "test";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "localhost";
        protected override DateTime DeadlineCore => DateTime.MaxValue;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => [];
        protected override Status StatusCore
        {
            get => Status.DefaultSuccess;
            set { }
        }
        protected override WriteOptions? WriteOptionsCore
        {
            get => null;
            set { }
        }
        protected override AuthContext AuthContextCore =>
            new(null, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            => throw new NotImplementedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            => Task.CompletedTask;
    }
}
