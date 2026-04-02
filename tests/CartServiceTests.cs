using cartservice.store;
using Grpc.Core;
using Hipstershop;
using Moq;
using Xunit;
using CartService = cartservice.services.CartService;

namespace cartservice.tests
{
    public class CartServiceTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        private static CartService BuildService(ICartStore store) => new(store);

        private static ServerCallContext FakeContext() => TestServerCallContext.Create();

        // ── AddItem ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task AddItem_DelegatesToStore()
        {
            Mock<ICartStore> store = new();
            CartService svc = BuildService(store.Object);

            await svc.AddItem(
                new AddItemRequest { UserId = "u1", Item = new CartItem { ProductId = "p1", Quantity = 2 } },
                FakeContext());

            store.Verify(s => s.AddItemAsync("u1", "p1", 2), Times.Once);
        }

        [Fact]
        public async Task AddItem_ReturnsEmptyProto()
        {
            Mock<ICartStore> store = new();
            CartService svc = BuildService(store.Object);

            Empty result = await svc.AddItem(
                new AddItemRequest { UserId = "u1", Item = new CartItem { ProductId = "p1", Quantity = 1 } },
                FakeContext());

            Assert.NotNull(result);
            Assert.IsType<Empty>(result);
        }

        [Fact]
        public async Task AddItem_StoreThrows_PropagatesRpcException()
        {
            Mock<ICartStore> store = new();
            store.Setup(s => s.AddItemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                 .ThrowsAsync(new RpcException(new Status(StatusCode.FailedPrecondition, "storage error")));

            CartService svc = BuildService(store.Object);

            RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
                svc.AddItem(
                    new AddItemRequest { UserId = "u1", Item = new CartItem { ProductId = "p1", Quantity = 1 } },
                    FakeContext()));

            Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        }

        // ── GetCart ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetCart_ReturnsCartFromStore()
        {
            Cart expected = new() { UserId = "u1" };
            expected.Items.Add(new CartItem { ProductId = "p1", Quantity = 3 });

            Mock<ICartStore> store = new();
            store.Setup(s => s.GetCartAsync("u1")).ReturnsAsync(expected);

            CartService svc = BuildService(store.Object);
            Cart cart = await svc.GetCart(new GetCartRequest { UserId = "u1" }, FakeContext());

            Assert.Equal("u1", cart.UserId);
            Assert.Single(cart.Items);
            Assert.Equal("p1", cart.Items[0].ProductId);
            Assert.Equal(3, cart.Items[0].Quantity);
        }

        [Fact]
        public async Task GetCart_EmptyUserId_ReturnsEmptyCart()
        {
            Mock<ICartStore> store = new();
            store.Setup(s => s.GetCartAsync("")).ReturnsAsync(new Cart { UserId = "" });

            CartService svc = BuildService(store.Object);
            Cart cart = await svc.GetCart(new GetCartRequest { UserId = "" }, FakeContext());

            Assert.NotNull(cart);
            Assert.Empty(cart.Items);
        }

        [Fact]
        public async Task GetCart_StoreThrows_PropagatesRpcException()
        {
            Mock<ICartStore> store = new();
            store.Setup(s => s.GetCartAsync(It.IsAny<string>()))
                 .ThrowsAsync(new RpcException(new Status(StatusCode.FailedPrecondition, "storage error")));

            CartService svc = BuildService(store.Object);

            RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
                svc.GetCart(new GetCartRequest { UserId = "u1" }, FakeContext()));

            Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        }

        // ── EmptyCart ─────────────────────────────────────────────────────────────

        [Fact]
        public async Task EmptyCart_DelegatesToStore()
        {
            Mock<ICartStore> store = new();
            CartService svc = BuildService(store.Object);

            await svc.EmptyCart(new EmptyCartRequest { UserId = "u1" }, FakeContext());

            store.Verify(s => s.EmptyCartAsync("u1"), Times.Once);
        }

        [Fact]
        public async Task EmptyCart_ReturnsEmptyProto()
        {
            Mock<ICartStore> store = new();
            CartService svc = BuildService(store.Object);

            Empty result = await svc.EmptyCart(new EmptyCartRequest { UserId = "u1" }, FakeContext());

            Assert.NotNull(result);
            Assert.IsType<Empty>(result);
        }

        [Fact]
        public async Task EmptyCart_StoreThrows_PropagatesRpcException()
        {
            Mock<ICartStore> store = new();
            store.Setup(s => s.EmptyCartAsync(It.IsAny<string>()))
                 .ThrowsAsync(new RpcException(new Status(StatusCode.FailedPrecondition, "storage error")));

            CartService svc = BuildService(store.Object);

            RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
                svc.EmptyCart(new EmptyCartRequest { UserId = "u1" }, FakeContext()));

            Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        }
    }
}
