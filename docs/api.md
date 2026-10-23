# API

Package `hipstershop`, service `CartService` ([`src/protos/cart.proto`](https://github.com/platform-boutique/app-cart-service/blob/main/src/protos/cart.proto)).

| Method | Request | Response | Behaviour |
|---|---|---|---|
| `AddItem` | `AddItemRequest{ user_id, CartItem{ product_id, quantity } }` | `Empty` | Creates the cart if absent; otherwise adds `quantity` to an existing line or appends a new one |
| `GetCart` | `GetCartRequest{ user_id }` | `Cart{ user_id, repeated CartItem items }` | Returns the cart, or an empty cart if none exists |
| `EmptyCart` | `EmptyCartRequest{ user_id }` | `Empty` | Overwrites the cart with an empty one |

Errors: any Redis failure is returned as gRPC `FAILED_PRECONDITION` ("Can't access cart storage. …").

### Storage model

- Redis key = `user_id` (raw, no prefix — the `IDistributedCache` instance has no `InstanceName`).
- Value = protobuf-serialised `Cart` (binary).
- **No TTL.** Carts live until Redis is wiped. `EmptyCart` writes an empty cart rather than deleting the key.
- `AddItem` is read-modify-write and not atomic: two concurrent adds for the same user can lose an update.
