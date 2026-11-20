using BenchmarkDotNet.Attributes;
using CacheShield.Application.Services;
using CacheShield.Core.Concurrency;
using CacheShield.Core.DataStructures;
using CacheShield.Core.Interfaces;

namespace CacheShield.Benchmarks;

[MemoryDiagnoser]
public class CacheShieldBenchmarks
{
    private LruCache _lru = null!;
    private HybridCacheService _hybrid = null!;
    private int _evictionCounter;

    [GlobalSetup]
    public void Setup()
    {
        var clock = new BenchmarkClock();
        _lru = new LruCache(capacity: 10_000, clock);

        // cache'i önceden 10.000 elemanla tamamen dolduruyoruz(worst case scenario)
        for (var i = 0; i < 10_000; i++)
        {
            _lru.Set($"key:{i}", $"value:{i}", TimeSpan.FromMinutes(10));
        }

        _hybrid = new HybridCacheService(_lru, new NoOpL2Cache(), new SingleflightLock());
    }

    [Benchmark(Baseline = true)]
    public bool LruCache_TryGet_Hit()
    {
        return _lru.TryGet<string>("key:5000", out _);
    }

    [Benchmark]
    public void LruCache_Set_UpdateExisting()
    {
        _lru.Set("key:5000", "updated-value", TimeSpan.FromMinutes(10));
    }

    [Benchmark]
    public void LruCache_Set_WithEviction()
    {
        var id = Interlocked.Increment(ref _evictionCounter);
        _lru.Set($"evict:{id}", "new-value", TimeSpan.FromMinutes(10));
    }

    [Benchmark]
    public ValueTask<string> HybridCache_L1_FastPath()
    {
        return _hybrid.GetOrCreateAsync(
            "key:5000",
            static _ => Task.FromResult("fallback"));
    }

    private sealed class BenchmarkClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class NoOpL2Cache : IL2Cache
    {
        public Task<(bool Found, T? Value)> TryGetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<(bool, T?)>((false, default));

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}