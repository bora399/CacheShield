using CacheShield.Application.Abstractions;
using CacheShield.Core.Concurrency;
using CacheShield.Core.Interfaces;
using CacheShield.Core.Models;

namespace CacheShield.Application.Services;

public sealed class HybridCacheService : ICacheShield
{
    private readonly IL1Cache _l1Cache;
    private readonly IL2Cache _l2Cache;
    private readonly SingleflightLock _singleflight;
    private readonly ICacheBackplane? _backplane;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    public HybridCacheService(
        IL1Cache l1Cache,
        IL2Cache l2Cache,
        SingleflightLock singleflight,
        ICacheBackplane? backplane = null)
    {
        ArgumentNullException.ThrowIfNull(l1Cache);
        ArgumentNullException.ThrowIfNull(l2Cache);
        ArgumentNullException.ThrowIfNull(singleflight);

        _l1Cache = l1Cache;
        _l2Cache = l2Cache;
        _singleflight = singleflight;
        _backplane = backplane;

        // diğer sunuculardan bu anahtar bayatladı anonsu gelirse ramden (l1) sil
        _backplane?.SubscribeInvalidation((key, originId) =>
        {
            if (!string.Equals(originId, _instanceId, StringComparison.Ordinal))
            {
                _l1Cache.Remove(key);
            }
        });
    }

    public ValueTask<T> GetOrCreateAsync<T>(
    string key,
    Func<CancellationToken, Task<T>> factory,
    CacheEntryOptions? options = null,
    CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        if (_l1Cache.TryGet<T>(key, out var l1Value))
        {
            return new ValueTask<T>(l1Value!);
        }

        return new ValueTask<T>(GetOrCreateSlowAsync(key, factory, options, cancellationToken));
    }

    private async Task<T> GetOrCreateSlowAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CacheEntryOptions? options,
        CancellationToken cancellationToken)
    {
        var entryOptions = options ?? CacheEntryOptions.Default;

        return await _singleflight.ExecuteAsync(key, async () =>
        {
            // Double-Check: Biz kapıda beklerken önceki istek L1'i doldurmuş olabilir
            if (_l1Cache.TryGet<T>(key, out var doubleCheckedL1Value))
            {
                return doubleCheckedL1Value!;
            }

            // L2'ye (Redis) bak — Fail-Open
            var (foundInL2, l2Value) = await TryGetFromL2SafeAsync<T>(key).ConfigureAwait(false);
            if (foundInL2)
            {
                _l1Cache.Set(key, l2Value!, entryOptions.L1Expiration);
                return l2Value!;
            }

            // İki katmanda da yok: Veritabanına (factory) yalnızca 1 kez git
            var freshValue = await factory(CancellationToken.None).ConfigureAwait(false);

            await SetToL2SafeAsync(key, freshValue, entryOptions.L2Expiration).ConfigureAwait(false);
            _l1Cache.Set(key, freshValue, entryOptions.L1Expiration);

            return freshValue;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        CacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var entryOptions = options ?? CacheEntryOptions.Default;

        // redisi güncelle
        await SetToL2SafeAsync(key, value, entryOptions.L2Expiration, cancellationToken).ConfigureAwait(false);

        // rami güncelle
        _l1Cache.Set(key, value, entryOptions.L1Expiration);

        // diğer sunuculara ramdeki eski veriyi silin anonsu çek
        await PublishInvalidationSafeAsync(key, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _l1Cache.Remove(key);

        await RemoveFromL2SafeAsync(key, cancellationToken).ConfigureAwait(false);

        await PublishInvalidationSafeAsync(key, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(bool Found, T? Value)> TryGetFromL2SafeAsync<T>(string key)
    {
        try
        {
            return await _l2Cache.TryGetAsync<T>(key, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            return (false, default);
        }
    }

    private async Task SetToL2SafeAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _l2Cache.SetAsync(key, value, ttl, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task RemoveFromL2SafeAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _l2Cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task PublishInvalidationSafeAsync(string key, CancellationToken cancellationToken)
    {
        if (_backplane is null)
        {
            return;
        }

        try
        {
            await _backplane.PublishInvalidationAsync(key, _instanceId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
    }
}