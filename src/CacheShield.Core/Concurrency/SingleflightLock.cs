using System.Collections.Concurrent;

namespace CacheShield.Core.Concurrency;

public sealed class SingleflightLock
{
    private readonly ConcurrentDictionary<string, object> _inFlight = new(StringComparer.Ordinal);

    public int InFlightCount => _inFlight.Count;

    public async Task<T> ExecuteAsync<T>(
        string key,
        Func<Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        cancellationToken.ThrowIfCancellationRequested();

        Lazy<Task<T>>? newEntry = null;
        newEntry = new Lazy<Task<T>>(
            () => RunAndCleanupAsync(key, newEntry!, factory),
            LazyThreadSafetyMode.ExecutionAndPublication);

        var activeEntry = _inFlight.GetOrAdd(key, newEntry);

        if (activeEntry is not Lazy<Task<T>> typedEntry)
        {
            throw new InvalidOperationException(
                $"'{key}' anahtarı şu anda farklı bir dönüş tipiyle işlemde.");
        }

        //tek bir lazy için çalışır. yoksa amacımızdan saparız. aynı ürüne gelen 1000 isteği aynı uçağa bindiriyoruz, SingleflightLock.
        var sharedTask = typedEntry.Value;

        //her isteğin kendi cancellationToken'ı vardır. birinin iptali uçuşu etkilemez.
        return await sharedTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> RunAndCleanupAsync<T>(
        string key,
        Lazy<Task<T>> entry,
        Func<Task<T>> factory)
    {
        try
        {
            return await factory().ConfigureAwait(false);
        }
        finally
        {
            _inFlight.TryRemove(new KeyValuePair<string, object>(key, entry));
        }
    }
}