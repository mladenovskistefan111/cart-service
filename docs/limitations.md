# Known limitations

- Carts never expire (no TTL) and `AddItem` is not atomic.
- Health is Redis-coupled; see probe guidance above.
- Default console logs are not JSON, so Loki sees unstructured lines unless the formatter is switched.
