using cartservice.store;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace cartservice.tests.Integration
{
    /// <summary>
    /// Integration tests for <see cref="RedisCartStore"/> that hit a real Redis instance.
    /// Skipped automatically when <c>REDIS_ADDR</c> is not set — mirrors the
    /// DATABASE_URL skip pattern used in db_integration_test.go.
    /// </summary>
    public class RedisCartStoreTests
    {
        private static readonly string? RedisAddr = Environment.GetEnvironmentVariable("REDIS_ADDR");

        private static RedisCartStore BuildStore()
        {
            RedisCacheOptions opts = new() { Configuration = RedisAddr };
            IDistributedCache cache = new RedisCache(Options.Create(opts));
            return new RedisCartStore(cache, NullLogger<RedisCartStore>.Instance);
        }

        // ── AddItem / GetCart ─────────────────────────────────────────────────────

        [SkippableFact]
        public async Task AddItem_NewProduct_AppearsInCart()
        {
            Skip.If(string.IsNullOrEmpty(RedisAddr), "REDIS_ADDR not set");

            RedisCartStore store = BuildStore();
            string userId = Guid.NewGuid().ToString();

            await store.AddItemAsync(userId, "prod-1", 1);

            Hipstershop.Cart cart = await store.GetCartAsync(userId);
            Assert.Single(cart.Items);
            Assert.Equal("prod-1", cart.Items[0].ProductId);
            Assert.Equal(1, cart.Items[0].Quantity);
        }

        [SkippableFact]
        public async Task AddItem_ExistingProduct_QuantityAccumulates()
        {
            Skip.If(string.IsNullOrEmpty(RedisAddr), "REDIS_ADDR not set");

            RedisCartStore store = BuildStore();
            string userId = Guid.NewGuid().ToString();

            await store.AddItemAsync(userId, "prod-1", 2);
            await store.AddItemAsync(userId, "prod-1", 3);

            Hipstershop.Cart cart = await store.GetCartAsync(userId);
            Assert.Single(cart.Items);
            Assert.Equal(5, cart.Items[0].Quantity);
        }

        [SkippableFact]
        public async Task AddItem_DifferentProducts_AllAppearInCart()
        {
            Skip.If(string.IsNullOrEmpty(RedisAddr), "REDIS_ADDR not set");

            RedisCartStore store = BuildStore();
            string userId = Guid.NewGuid().ToString();

            await store.AddItemAsync(userId, "prod-1", 1);
            await store.AddItemAsync(userId, "prod-2", 1);

            Hipstershop.Cart cart = await store.GetCartAsync(userId);
            Assert.Equal(2, cart.Items.Count);
        }

        // ── GetCart ───────────────────────────────────────────────────────────────

        [SkippableFact]
        public async Task GetCart_NoItemsAdded_ReturnsEmptyCart()
        {
            Skip.If(string.IsNullOrEmpty(RedisAddr), "REDIS_ADDR not set");

            RedisCartStore store = BuildStore();
            string userId = Guid.NewGuid().ToString();

            Hipstershop.Cart cart = await store.GetCartAsync(userId);

            Assert.NotNull(cart);
            Assert.Empty(cart.Items);
        }

        // ── EmptyCart ─────────────────────────────────────────────────────────────

        [SkippableFact]
        public async Task EmptyCart_ClearsAllItems()
        {
            Skip.If(string.IsNullOrEmpty(RedisAddr), "REDIS_ADDR not set");

            RedisCartStore store = BuildStore();
            string userId = Guid.NewGuid().ToString();

            await store.AddItemAsync(userId, "prod-1", 1);
            await store.AddItemAsync(userId, "prod-2", 2);
            await store.EmptyCartAsync(userId);

            Hipstershop.Cart cart = await store.GetCartAsync(userId);
            Assert.Empty(cart.Items);
        }

        // ── Ping ──────────────────────────────────────────────────────────────────

        [SkippableFact]
        public async Task Ping_ReachableRedis_ReturnsTrue()
        {
            Skip.If(string.IsNullOrEmpty(RedisAddr), "REDIS_ADDR not set");

            RedisCartStore store = BuildStore();
            Assert.True(await store.PingAsync());
        }
    }
}
