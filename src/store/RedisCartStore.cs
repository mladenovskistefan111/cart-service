using Google.Protobuf;
using Grpc.Core;
using Hipstershop;
using Microsoft.Extensions.Caching.Distributed;

namespace cartservice.store
{
    public partial class RedisCartStore(IDistributedCache cache, ILogger<RedisCartStore> logger) : ICartStore
    {
        // LoggerMessage delegates — CA1848
        [LoggerMessage(Level = LogLevel.Information, Message = "AddItem called for userId={UserId} productId={ProductId} quantity={Quantity}")]
        private static partial void LogAddItem(ILogger logger, string userId, string productId, int quantity);

        [LoggerMessage(Level = LogLevel.Information, Message = "GetCart called for userId={UserId}")]
        private static partial void LogGetCart(ILogger logger, string userId);

        [LoggerMessage(Level = LogLevel.Information, Message = "EmptyCart called for userId={UserId}")]
        private static partial void LogEmptyCart(ILogger logger, string userId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add item to cart for userId={UserId}")]
        private static partial void LogAddItemError(ILogger logger, Exception ex, string userId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get cart for userId={UserId}")]
        private static partial void LogGetCartError(ILogger logger, Exception ex, string userId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to empty cart for userId={UserId}")]
        private static partial void LogEmptyCartError(ILogger logger, Exception ex, string userId);

        public async Task AddItemAsync(string userId, string productId, int quantity)
        {
            LogAddItem(logger, userId, productId, quantity);

            try
            {
                Cart cart;
                byte[]? bytes = await cache.GetAsync(userId);

                if (bytes == null)
                {
                    cart = new Cart { UserId = userId };
                    cart.Items.Add(new CartItem { ProductId = productId, Quantity = quantity });
                }
                else
                {
                    cart = Cart.Parser.ParseFrom(bytes);
                    CartItem? existing = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                    if (existing == null)
                    {
                        cart.Items.Add(new CartItem { ProductId = productId, Quantity = quantity });
                    }
                    else
                    {
                        existing.Quantity += quantity;
                    }
                }

                await cache.SetAsync(userId, cart.ToByteArray());
            }
            catch (Exception ex)
            {
                LogAddItemError(logger, ex, userId);
                throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    $"Can't access cart storage. {ex.Message}"));
            }
        }

        public async Task<Cart> GetCartAsync(string userId)
        {
            LogGetCart(logger, userId);

            try
            {
                byte[]? bytes = await cache.GetAsync(userId);
                return bytes != null ? Cart.Parser.ParseFrom(bytes) : new Cart { UserId = userId };
            }
            catch (Exception ex)
            {
                LogGetCartError(logger, ex, userId);
                throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    $"Can't access cart storage. {ex.Message}"));
            }
        }

        public async Task EmptyCartAsync(string userId)
        {
            LogEmptyCart(logger, userId);

            try
            {
                await cache.SetAsync(userId, new Cart { UserId = userId }.ToByteArray());
            }
            catch (Exception ex)
            {
                LogEmptyCartError(logger, ex, userId);
                throw new RpcException(new Status(StatusCode.FailedPrecondition,
                    $"Can't access cart storage. {ex.Message}"));
            }
        }

        public async Task<bool> PingAsync()
        {
            try
            {
                await cache.GetAsync("health-ping");
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
