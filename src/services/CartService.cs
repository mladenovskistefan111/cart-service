using Grpc.Core;
using Hipstershop;
using cartservice.store;

namespace cartservice.services;

public class CartService : Hipstershop.CartService.CartServiceBase
{
    private static readonly Empty Empty = new();
    private readonly RedisCartStore _store;
    private readonly ILogger<CartService> _logger;

    public CartService(RedisCartStore store, ILogger<CartService> logger)
    {
        _store  = store;
        _logger = logger;
    }

    public override async Task<Empty> AddItem(AddItemRequest request, ServerCallContext context)
    {
        await _store.AddItemAsync(request.UserId, request.Item.ProductId, request.Item.Quantity);
        return Empty;
    }

    public override async Task<Cart> GetCart(GetCartRequest request, ServerCallContext context)
    {
        return await _store.GetCartAsync(request.UserId);
    }

    public override async Task<Empty> EmptyCart(EmptyCartRequest request, ServerCallContext context)
    {
        await _store.EmptyCartAsync(request.UserId);
        return Empty;
    }
}