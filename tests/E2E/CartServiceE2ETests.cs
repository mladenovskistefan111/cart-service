using Grpc.Net.Client;
using Xunit;

namespace cartservice.tests.E2E
{
    public sealed class CartServiceE2ETests : IDisposable
    {
        private static string GrpcHost =>
            Environment.GetEnvironmentVariable("GRPC_HOST") ?? "localhost:7070";

        private readonly GrpcChannel _channel;
        private readonly Hipstershop.CartService.CartServiceClient _client;
        private readonly bool _serviceReachable;

        public CartServiceE2ETests()
        {
            _channel = GrpcChannel.ForAddress($"http://{GrpcHost}");
            _client = new Hipstershop.CartService.CartServiceClient(_channel);

            try
            {
                _client.GetCart(new Hipstershop.GetCartRequest { UserId = "probe" });
                _serviceReachable = true;
            }
            catch
            {
                _serviceReachable = false;
            }
        }

        public void Dispose() => _channel.Dispose();

        [SkippableFact]
        public void GetCart_NewUser_ReturnsEmptyCart()
        {
            Skip.If(!_serviceReachable, $"cart-service not reachable at {GrpcHost}");

            string userId = Guid.NewGuid().ToString();
            Hipstershop.Cart cart = _client.GetCart(new Hipstershop.GetCartRequest { UserId = userId });

            Assert.NotNull(cart);
            Assert.Empty(cart.Items);
        }

        [SkippableFact]
        public void AddItem_NewItem_AppearsInCart()
        {
            Skip.If(!_serviceReachable, $"cart-service not reachable at {GrpcHost}");

            string userId = Guid.NewGuid().ToString();

            _client.AddItem(new Hipstershop.AddItemRequest
            {
                UserId = userId,
                Item = new Hipstershop.CartItem { ProductId = "OLJCESPC7Z", Quantity = 1 }
            });

            Hipstershop.Cart cart = _client.GetCart(new Hipstershop.GetCartRequest { UserId = userId });
            Assert.Single(cart.Items);
            Assert.Equal("OLJCESPC7Z", cart.Items[0].ProductId);

            _client.EmptyCart(new Hipstershop.EmptyCartRequest { UserId = userId });
        }

        [SkippableFact]
        public void AddItem_SameProductTwice_QuantityAccumulates()
        {
            Skip.If(!_serviceReachable, $"cart-service not reachable at {GrpcHost}");

            string userId = Guid.NewGuid().ToString();
            Hipstershop.AddItemRequest req = new()
            {
                UserId = userId,
                Item = new Hipstershop.CartItem { ProductId = "OLJCESPC7Z", Quantity = 1 }
            };

            _client.AddItem(req);
            _client.AddItem(req);

            Hipstershop.Cart cart = _client.GetCart(new Hipstershop.GetCartRequest { UserId = userId });
            Assert.Single(cart.Items);
            Assert.Equal(2, cart.Items[0].Quantity);

            _client.EmptyCart(new Hipstershop.EmptyCartRequest { UserId = userId });
        }

        [SkippableFact]
        public void EmptyCart_ClearsAllItems()
        {
            Skip.If(!_serviceReachable, $"cart-service not reachable at {GrpcHost}");

            string userId = Guid.NewGuid().ToString();

            _client.AddItem(new Hipstershop.AddItemRequest
            {
                UserId = userId,
                Item = new Hipstershop.CartItem { ProductId = "OLJCESPC7Z", Quantity = 2 }
            });

            _client.EmptyCart(new Hipstershop.EmptyCartRequest { UserId = userId });

            Hipstershop.Cart cart = _client.GetCart(new Hipstershop.GetCartRequest { UserId = userId });
            Assert.Empty(cart.Items);
        }

        [SkippableFact]
        public void AddMultipleProducts_AllAppearInCart()
        {
            Skip.If(!_serviceReachable, $"cart-service not reachable at {GrpcHost}");

            string userId = Guid.NewGuid().ToString();

            _client.AddItem(new Hipstershop.AddItemRequest
            {
                UserId = userId,
                Item = new Hipstershop.CartItem { ProductId = "OLJCESPC7Z", Quantity = 1 }
            });
            _client.AddItem(new Hipstershop.AddItemRequest
            {
                UserId = userId,
                Item = new Hipstershop.CartItem { ProductId = "66VCHSJNUP", Quantity = 3 }
            });

            Hipstershop.Cart cart = _client.GetCart(new Hipstershop.GetCartRequest { UserId = userId });
            Assert.Equal(2, cart.Items.Count);

            _client.EmptyCart(new Hipstershop.EmptyCartRequest { UserId = userId });
        }
    }
}
