using CacheShield.Core.Interfaces;

namespace CacheShield.UnitTests.Fakes;

public sealed class FakeBackplane : ICacheBackplane
{
    private readonly List<Action<string, string>> _subscribers = new();
    private readonly List<string> _publishedKeys = new();

    public IReadOnlyList<string> PublishedKeys => _publishedKeys;

    public Task PublishInvalidationAsync(string key, string originId, CancellationToken cancellationToken = default)
    {
        _publishedKeys.Add(key);

        foreach (var subscriber in _subscribers)
        {
            subscriber(key, originId);
        }

        return Task.CompletedTask;
    }

    public void SubscribeInvalidation(Action<string, string> onKeyInvalidated)
    {
        _subscribers.Add(onKeyInvalidated);
    }

    public void SimulateRemoteInvalidation(string key)
    {
        foreach (var subscriber in _subscribers)
        {
            subscriber(key, "remote-node");
        }
    }
}