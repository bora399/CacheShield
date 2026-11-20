using CacheShield.Application.Services;
using CacheShield.Core.Concurrency;
using CacheShield.Core.DataStructures;
using CacheShield.UnitTests.Fakes;

namespace CacheShield.UnitTests;

public class HybridCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_WhenInL1_ShouldReturnFromL1WithoutCallingL2OrFactory()
    {
        var clock = new FakeClock();
        var l1 = new LruCache(10, clock);
        var l2 = new FakeL2Cache();
        var service = new HybridCacheService(l1, l2, new SingleflightLock());

        l1.Set("product:1", "Mekanik Klavye", TimeSpan.FromMinutes(5));
        var factoryCalled = false;

        var result = await service.GetOrCreateAsync("product:1", _ =>
        {
            factoryCalled = true;
            return Task.FromResult("DB'den Gelen");
        });

        Assert.Equal("Mekanik Klavye", result);
        Assert.False(factoryCalled);
        Assert.Equal(0, l2.GetCallCount); // fast Path
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenL1MissAndL2Hit_ShouldPopulateL1AndSkipFactory()
    {
        var clock = new FakeClock();
        var l1 = new LruCache(10, clock);
        var l2 = new FakeL2Cache();
        var service = new HybridCacheService(l1, l2, new SingleflightLock());

        // veri sadece l2de var
        await l2.SetAsync("product:2", "Mouse", TimeSpan.FromMinutes(10));
        var factoryCalled = false;

        var result = await service.GetOrCreateAsync("product:2", _ =>
        {
            factoryCalled = true;
            return Task.FromResult("DB'den Gelen");
        });

        Assert.Equal("Mouse", result);
        Assert.False(factoryCalled);
        Assert.Equal(1, l2.GetCallCount);

        // l2den okunan veri l1e yazılmış olmalı
        Assert.True(l1.TryGet<string>("product:2", out var cachedInL1));
        Assert.Equal("Mouse", cachedInL1);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenBothMiss_ShouldCallFactoryOnceAndPopulateL1AndL2()
    {
        var clock = new FakeClock();
        var l1 = new LruCache(10, clock);
        var l2 = new FakeL2Cache();
        var service = new HybridCacheService(l1, l2, new SingleflightLock());
        var factoryCalls = 0;

        // 500 eşzamanlı istek aynı anda geliyor
        var tasks = Enumerable.Range(0, 500)
            .Select(_ => service.GetOrCreateAsync("product:3", async _ =>
            {
                Interlocked.Increment(ref factoryCalls);
                await Task.Delay(50);
                return "Monitör";
            }).AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, factoryCalls);       // Veritabanına sadece 1 kez gidildi
        Assert.Equal(1, l2.GetCallCount);    // Redis'e sadece 1 kez soruldu
        Assert.Equal(1, l2.SetCallCount);    // Redis'e sadece 1 kez yazıldı
        Assert.All(results, r => Assert.Equal("Monitör", r));
        Assert.True(l1.TryGet<string>("product:3", out var l1Value));
        Assert.Equal("Monitör", l1Value);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenL2Throws_ShouldFailOpenAndUseFactoryAndL1()
    {
        var clock = new FakeClock();
        var l1 = new LruCache(10, clock);
        var l2 = new FakeL2Cache { SimulateOutage = true }; // Redis çöktü!
        var service = new HybridCacheService(l1, l2, new SingleflightLock());
        var factoryCalls = 0;

        // redis hata fırlatsa bile sistem çökmeden dbden veriyi almalı
        var firstResult = await service.GetOrCreateAsync("product:4", _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult("Kulaklık");
        });

        // veri artık ramde olduğu için dbye de gitmeden ramden dönmeli
        var secondResult = await service.GetOrCreateAsync("product:4", _ =>
        {
            Interlocked.Increment(ref factoryCalls);
            return Task.FromResult("Kulaklık");
        });

        Assert.Equal("Kulaklık", firstResult);
        Assert.Equal("Kulaklık", secondResult);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task RemoteInvalidation_ShouldRemoveStaleEntryFromLocalL1()
    {
        var clock = new FakeClock();
        var l1 = new LruCache(10, clock);
        var l2 = new FakeL2Cache();
        var backplane = new FakeBackplane();
        _ = new HybridCacheService(l1, l2, new SingleflightLock(), backplane);

        l1.Set("product:5", "Eski Fiyat: 100 TL", TimeSpan.FromMinutes(5));
        Assert.True(l1.TryGet<string>("product:5", out _));

        backplane.SimulateRemoteInvalidation("product:5");

        Assert.False(l1.TryGet<string>("product:5", out _));
    }

    [Fact]
    public async Task RemoveAsync_ShouldClearL1AndL2AndPublishInvalidation()
    {
        var clock = new FakeClock();
        var l1 = new LruCache(10, clock);
        var l2 = new FakeL2Cache();
        var backplane = new FakeBackplane();
        var service = new HybridCacheService(l1, l2, new SingleflightLock(), backplane);

        await service.SetAsync("product:6", "Mikrofon");
        Assert.True(l1.TryGet<string>("product:6", out _));

        await service.RemoveAsync("product:6");

        Assert.False(l1.TryGet<string>("product:6", out _));
        var (foundInL2, _) = await l2.TryGetAsync<string>("product:6");
        Assert.False(foundInL2);
        Assert.Contains("product:6", backplane.PublishedKeys);
    }
}