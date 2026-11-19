using CacheShield.Core.Concurrency;

namespace CacheShield.UnitTests;

public class SingleflightLockTests
{
    [Fact]
    public async Task ExecuteAsync_With1000ConcurrentRequests_ShouldInvokeFactoryOnlyOnce()
    {
        var singleflight = new SingleflightLock();
        var factoryCallCount = 0;

        // 1000 istek aynı anda 42 nolu ürünü ister
        var tasks = Enumerable.Range(0, 1_000)
            .Select(_ => singleflight.ExecuteAsync("product:42", async () =>
            {
                Interlocked.Increment(ref factoryCallCount);

                await Task.Delay(100);
                return "Espresso";
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, factoryCallCount);

        Assert.All(results, result => Assert.Equal("Espresso", result));

        Assert.Equal(0, singleflight.InFlightCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneCallerCancels_ShouldNotAffectOtherWaiters()
    {
        var singleflight = new SingleflightLock();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var cts = new CancellationTokenSource();

        // iki kullanıcı aynı anahtarı bekliyor
        var caller1Task = singleflight.ExecuteAsync("product:99", () => tcs.Task, cts.Token);
        var caller2Task = singleflight.ExecuteAsync("product:99", () => tcs.Task, CancellationToken.None);

        // 1. kullanıcı tarayıcıyı kapattı
        cts.Cancel();

        // 1. kullanıcı iptal hatası almalı
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => caller1Task);

        // database yanıtını döndürdüğünde 2. kullanıcı verisini sorunsuz almalı
        tcs.SetResult("Latte");
        var caller2Result = await caller2Task;

        Assert.Equal("Latte", caller2Result);
        Assert.Equal(0, singleflight.InFlightCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFactoryThrows_ShouldCleanupAndAllowRetry()
    {
        var singleflight = new SingleflightLock();
        var attempt = 0;

        // 1. denemede veritabanı hata fırlatır
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            singleflight.ExecuteAsync<string>("product:err", () =>
            {
                Interlocked.Increment(ref attempt);
                return Task.FromException<string>(new InvalidOperationException("DB down"));
            }));

        // hata sonrası uçuş kaydı temizlenmiş olmalı
        Assert.Equal(0, singleflight.InFlightCount);

        // 2. denemede sistem toparlanıp veriyi çekebilmeli
        var result = await singleflight.ExecuteAsync("product:err", () =>
        {
            Interlocked.Increment(ref attempt);
            return Task.FromResult("Recovered");
        });

        Assert.Equal("Recovered", result);
        Assert.Equal(2, attempt);
    }
}