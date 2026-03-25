using Google.Protobuf;
using Grpc.Core;
using Hipstershop;
using Microsoft.Extensions.Caching.Distributed;

namespace cartservice.store;

public class RedisCartStore
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCartStore> _logger;

    public RedisCartStore(IDistributedCache cache, ILogger<RedisCartStore> logger)
    {
        _cache  = cache;
        _logger = logger;
    }

    public async Task AddItemAsync(string userId, string productId, int quantity)
    {
        _logger.LogInformation("AddItem called for userId={UserId} productId={ProductId} quantity={Quantity}",
            userId, productId, quantity);

        try
        {
            Cart cart;
            var bytes = await _cache.GetAsync(userId);

            if (bytes == null)
            {
                cart = new Cart { UserId = userId };
                cart.Items.Add(new CartItem { ProductId = productId, Quantity = quantity });
            }
            else
            {
                cart = Cart.Parser.ParseFrom(bytes);
                var existing = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                if (existing == null)
                    cart.Items.Add(new CartItem { ProductId = productId, Quantity = quantity });
                else
                    existing.Quantity += quantity;
            }

            await _cache.SetAsync(userId, cart.ToByteArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add item to cart for userId={UserId}", userId);
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                $"Can't access cart storage. {ex.Message}"));
        }
    }

    public async Task<Cart> GetCartAsync(string userId)
    {
        _logger.LogInformation("GetCart called for userId={UserId}", userId);

        try
        {
            var bytes = await _cache.GetAsync(userId);
            if (bytes != null)
                return Cart.Parser.ParseFrom(bytes);

            return new Cart { UserId = userId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cart for userId={UserId}", userId);
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                $"Can't access cart storage. {ex.Message}"));
        }
    }

    public async Task EmptyCartAsync(string userId)
    {
        _logger.LogInformation("EmptyCart called for userId={UserId}", userId);

        try
        {
            await _cache.SetAsync(userId, new Cart { UserId = userId }.ToByteArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to empty cart for userId={UserId}", userId);
            throw new RpcException(new Status(StatusCode.FailedPrecondition,
                $"Can't access cart storage. {ex.Message}"));
        }
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            await _cache.GetAsync("health-ping");
            return true;
        }
        catch
        {
            return false;
        }
    }
}