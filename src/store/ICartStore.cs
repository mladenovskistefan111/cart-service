using Hipstershop;

namespace cartservice.store
{
    /// <summary>
    /// Abstraction over the cart persistence layer.
    /// Implemented by <see cref="RedisCartStore"/>; can be replaced with an
    /// in-memory stub in unit tests without touching infrastructure.
    /// </summary>
    public interface ICartStore
    {
        Task AddItemAsync(string userId, string productId, int quantity);
        Task<Cart> GetCartAsync(string userId);
        Task EmptyCartAsync(string userId);
        Task<bool> PingAsync();
    }
}
