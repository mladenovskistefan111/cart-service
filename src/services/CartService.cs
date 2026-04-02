using Grpc.Core;
using Hipstershop;
using cartservice.store;

namespace cartservice.services
{
    public class CartService(ICartStore store) : Hipstershop.CartService.CartServiceBase
    {
        private static readonly Empty Empty = new();

        public override async Task<Empty> AddItem(AddItemRequest request, ServerCallContext context)
        {
            await store.AddItemAsync(request.UserId, request.Item.ProductId, request.Item.Quantity);
            return Empty;
        }

        public override async Task<Cart> GetCart(GetCartRequest request, ServerCallContext context)
        {
            return await store.GetCartAsync(request.UserId);
        }

        public override async Task<Empty> EmptyCart(EmptyCartRequest request, ServerCallContext context)
        {
            await store.EmptyCartAsync(request.UserId);
            return Empty;
        }
    }
}
