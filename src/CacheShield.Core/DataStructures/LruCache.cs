using CacheShield.Core.Interfaces;

namespace CacheShield.Core.DataStructures;

public sealed class LruCache : IL1Cache
{
    private readonly int _capacity;
    private readonly IClock _clock;
    private readonly Dictionary<string, CacheNode> _map;
    private readonly CacheNode _head;
    private readonly CacheNode _tail;
    private readonly object _lock = new();

    public LruCache(int capacity, IClock clock)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentNullException.ThrowIfNull(clock);

        _capacity = capacity;
        _clock = clock;
        _map = new Dictionary<string, CacheNode>(capacity);

        _head = new CacheNode(string.Empty, null, DateTimeOffset.MaxValue);//fake dugumler, sınır durumlar icin
        _tail = new CacheNode(string.Empty, null, DateTimeOffset.MaxValue);

        _head.Next = _tail;
        _tail.Prev = _head;
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _map.Count;
            }
        }
    }

    public bool TryGet<T>(string key, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_lock)
        {
            if (!_map.TryGetValue(key, out var node))
            {
                value = default;
                return false;
            }

            //süresi dolduysa node'dan cıkarıyoruz
            if (_clock.UtcNow >= node.ExpiresAt)
            {
                RemoveNode(node);
                _map.Remove(key);
                value = default;
                return false;
            }

            MoveToHead(node);

            if (node.Value is T typedValue)
            {
                value = typedValue;
                return true;
            }

            if (node.Value is null && default(T) is null)
            {
                value = default;
                return true;
            }

            value = default;
            return false;
        }
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL süresi sıfırdan büyük olmalıdır.");
        }

        var expiresAt = _clock.UtcNow.Add(ttl);

        lock (_lock)
        {
            //anahtarın zaten var oldugu senaryo
            if (_map.TryGetValue(key, out var existingNode))
            {
                existingNode.Value = value;
                existingNode.ExpiresAt = expiresAt;
                MoveToHead(existingNode);
                return;
            }

            //kapasite doluysa kuyruktaki en eskiyi kaldır
            if (_map.Count >= _capacity)
            {
                var lruNode = _tail.Prev!;
                if (lruNode != _head)
                {
                    RemoveNode(lruNode);
                    _map.Remove(lruNode.Key);
                }
            }

            //dugumu olustur ve basa ekle
            var newNode = new CacheNode(key, value, expiresAt);
            _map[key] = newNode;
            AddToHead(newNode);
        }
    }

    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_lock)
        {
            if (_map.Remove(key, out var node))
            {
                RemoveNode(node);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _map.Clear();
            _head.Next = _tail;
            _tail.Prev = _head;
        }
    }

    private void AddToHead(CacheNode node)
    {
        node.Prev = _head;
        node.Next = _head.Next;//yeni dugumun sol eli heade, sağ elini eski ilk elemana, eski ilk elemanın sol elini headden alıp yeni düğüme, en son da headin sağ elini yeni düğüme ver.
        _head.Next!.Prev = node;
        _head.Next = node;
    }

    private static void RemoveNode(CacheNode node)
    {
        node.Prev!.Next = node.Next;
        node.Next!.Prev = node.Prev;

        //garbage collector icin nulluyoruz
        node.Prev = null;
        node.Next = null;
    }

    private void MoveToHead(CacheNode node)
    {
        RemoveNode(node);
        AddToHead(node);
    }

    private sealed class CacheNode
    {
        public string Key { get; }
        public object? Value { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public CacheNode? Prev { get; set; }
        public CacheNode? Next { get; set; }

        public CacheNode(string key, object? value, DateTimeOffset expiresAt)
        {
            Key = key;
            Value = value;
            ExpiresAt = expiresAt;
        }
    }
}