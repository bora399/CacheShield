# CacheShield

Two-level hybrid caching library (in-memory LRU + Redis) for .NET 8 with cache stampede protection.

## What it does

Most cache setups either hit Redis on every single read or leave the database exposed when a hot key expires under load. CacheShield handles both in a small library:

- Checks a custom O(1) in-memory LRU cache (L1) first before touching the network.
- Uses key-based singleflight locking on misses so concurrent requests for the same key wait for a single fetch instead of hammering the database.
- Falls back to Redis (L2) and uses Redis Pub/Sub (with per-instance origin filtering) to invalidate stale L1 keys across instances.
- Fails open when Redis is unreachable, falling back to L1 and the data source instead of throwing on every request.

## Usage

Register it in `Program.cs`:

```csharp
using CacheShield.Extensions.DependencyInjection;

builder.Services.AddCacheShield(options =>
{
    options.L1Capacity = 10_000;
    options.RedisConnectionString = "localhost:6379";
});
```

Inject `ICacheShield` where you need it:

```csharp
using CacheShield.Application.Abstractions;
using CacheShield.Core.Models;

public class ProductService(ICacheShield cache, IProductRepository db)
{
    public ValueTask<Product?> GetByIdAsync(int id, CancellationToken ct)
    {
        var options = new CacheEntryOptions(
            L1Expiration: TimeSpan.FromMinutes(1),
            L2Expiration: TimeSpan.FromMinutes(15));

        return cache.GetOrCreateAsync(
            $"product:{id}",
            token => db.FindByIdAsync(id, token),
            options,
            ct);
    }

    public async Task UpdateAsync(Product product, CancellationToken ct)
    {
        await db.UpdateAsync(product, ct);
        await cache.SetAsync($"product:{product.Id}", product, cancellationToken: ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        await db.DeleteAsync(id, ct);
        await cache.RemoveAsync($"product:{id}", ct);
    }
}
```

## Project layout

- `src/CacheShield.Core` — O(1) LRU cache, singleflight lock, and core interfaces (no external dependencies)
- `src/CacheShield.Application` — hybrid read/write flow and fail-open handling
- `src/CacheShield.Infrastructure.Redis` — Redis L2 storage, Pub/Sub backplane, and UTF-8 JSON serializer
- `src/CacheShield.Extensions.DependencyInjection` — `AddCacheShield` registration and option validation
- `tests/CacheShield.UnitTests` — unit and concurrency tests (xUnit)
- `tests/CacheShield.Benchmarks` — BenchmarkDotNet microbenchmarks

## Benchmarks

Run on .NET 8.0 (i5-11400H, Windows 11) with a pre-filled 10,000-entry L1 cache:

| Method                      | Mean      | StdDev   | Allocated |
| --------------------------- | --------: | -------: | --------: |
| LruCache_TryGet_Hit         |  59.65 ns | 0.130 ns |         - |
| LruCache_Set_UpdateExisting |  56.36 ns | 0.457 ns |         - |
| LruCache_Set_WithEviction   | 166.57 ns | 2.516 ns |     120 B |
| HybridCache_L1_FastPath     |  69.55 ns | 0.257 ns |         - |

`GetOrCreateAsync` uses `ValueTask<T>` with a synchronous L1 fast path, which brought L1 hits down from ~97 ns (152 B) to ~69 ns with zero allocations.

## Running tests & benchmarks

```bash
dotnet test
dotnet run -c Release --project tests/CacheShield.Benchmarks
```

## License
MIT