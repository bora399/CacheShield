using CacheShield.Core.DataStructures;
using CacheShield.UnitTests.Fakes;

namespace CacheShield.UnitTests;

public class LruCacheTests
{
    [Fact]
    public void Set_And_TryGet_ShouldReturnStoredValue()
    {
        var clock = new FakeClock();
        var cache = new LruCache(capacity: 3, clock);

        cache.Set("urun:1", "Klavye", TimeSpan.FromMinutes(5));

        var found = cache.TryGet<string>("urun:1", out var value);

        Assert.True(found);
        Assert.Equal("Klavye", value);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Set_WhenCapacityExceeded_ShouldEvictLeastRecentlyUsedItem()
    {
        var clock = new FakeClock();
        var cache = new LruCache(capacity: 2, clock);

        cache.Set("k1", "v1", TimeSpan.FromMinutes(5));
        cache.Set("k2", "v2", TimeSpan.FromMinutes(5));

        // kapasite 2. k3 gelince en eski olan k1 atılmalı
        cache.Set("k3", "v3", TimeSpan.FromMinutes(5));

        Assert.False(cache.TryGet<string>("k1", out _));
        Assert.True(cache.TryGet<string>("k2", out var v2));
        Assert.True(cache.TryGet<string>("k3", out var v3));
        Assert.Equal("v2", v2);
        Assert.Equal("v3", v3);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void TryGet_ShouldRefreshRecency_AndEvictOtherItem()
    {
        var clock = new FakeClock();
        var cache = new LruCache(capacity: 2, clock);

        cache.Set("k1", "v1", TimeSpan.FromMinutes(5));
        cache.Set("k2", "v2", TimeSpan.FromMinutes(5));

        // k1 okunduğu için listenin başına (Head) taşınmalı; artık en eski k2 olur
        Assert.True(cache.TryGet<string>("k1", out _));

        cache.Set("k3", "v3", TimeSpan.FromMinutes(5));

        Assert.True(cache.TryGet<string>("k1", out _));
        Assert.False(cache.TryGet<string>("k2", out _));
        Assert.True(cache.TryGet<string>("k3", out _));
    }

    [Fact]
    public void TryGet_WhenItemExpired_ShouldReturnFalseAndRemoveItem()
    {
        var clock = new FakeClock();
        var cache = new LruCache(capacity: 5, clock);

        cache.Set("otp:123", "998877", TimeSpan.FromSeconds(30));

        // 29. saniyede hâlâ geçerli olmalı
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.True(cache.TryGet<string>("otp:123", out _));

        // 1 saniye daha ileri al -> 30. saniyede Lazy Expiration devreye girmeli
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(cache.TryGet<string>("otp:123", out var expiredValue));
        Assert.Null(expiredValue);
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void ConcurrentOperations_ShouldRemainThreadSafe()
    {
        var clock = new FakeClock();
        var cache = new LruCache(capacity: 100, clock);

        // farklı thread'lerden aynı anda 10.000 okuma/yazma/silme saldırısı
        Parallel.For(0, 10_000, i =>
        {
            var key = $"key:{i % 150}";
            cache.Set(key, i, TimeSpan.FromMinutes(1));
            cache.TryGet<int>(key, out _);

            if (i % 7 == 0)
            {
                cache.Remove(key);
            }
        });

        // kilit mekanizması düzgün çalıştıysa kapasite asla aşılmaz ve exception fırlamaz
        Assert.InRange(cache.Count, 0, 100);
    }
}