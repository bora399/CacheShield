# CacheShield

Two-level hybrid caching library (in-memory LRU + Redis) for .NET 8 with basic cache stampede protection.

*Work in progress.*

## What it does

Most cache setups either hit Redis on every single read or leave the database exposed when a hot key expires under load. CacheShield tries to handle both in a small library:

- Checks a custom O(1) in-memory LRU cache (L1) first before touching the network.
- Uses key-based locking (singleflight) on cache misses so concurrent requests for the same key wait for a single fetch instead of hammering the database.
- Falls back to Redis (L2) and uses Redis Pub/Sub to invalidate stale L1 keys across instances.

## Project layout

- `CacheShield.Core` — LRU cache, singleflight lock, and core interfaces (no external dependencies)
- `CacheShield.Application` — hybrid read/write flow
- `CacheShield.Infrastructure.Redis` — Redis storage, Pub/Sub backplane, and JSON serializer
- `CacheShield.Extensions.DependencyInjection` — DI registration

## Target API

```csharp
builder.Services.AddCacheShield(options =>
{
    options.L1Capacity = 10_000;
    options.RedisConnectionString = "localhost:6379";
});

var product = await cache.GetOrCreateAsync(
    $"product:{id}",
    ct => db.Products.FindAsync(id, ct),
    cancellationToken: ct);
```

## License

MIT