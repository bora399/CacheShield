using System.Collections.Concurrent;
using CacheShield.Core.Interfaces;

namespace CacheShield.UnitTests.Fakes;

public sealed class FakeL2Cache : IL2Cache
{
    private readonly ConcurrentDictionary<string, object?> _store = new(StringComparer.Ordinal);
    private int _getCallCount;
    private int _setCallCount;

    public bool SimulateOutage { get; set; }
    public int GetCallCount => _getCallCount;
    public int SetCallCount => _setCallCount;

    public Task<(bool Found, T? Value)> TryGetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _getCallCount);

        if (SimulateOutage)
        {
            throw new TimeoutException("redis bağlantısı koptu");
        }

        if (_store.TryGetValue(key, out var raw) && raw is T typed)
        {
            return Task.FromResult<(bool, T?)>((true, typed));
        }

        return Task.FromResult<(bool, T?)>((false, default));
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _setCallCount);

        if (SimulateOutage)
        {
            throw new TimeoutException("redis yazma hatası");
        }

        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (SimulateOutage)
        {
            throw new TimeoutException("redis silme hatası");
        }

        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}