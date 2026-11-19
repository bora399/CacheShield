using CacheShield.Core.Interfaces;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Infrastructure.Redis
{
    public sealed class RedisL2Cache : IL2Cache
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ICacheSerializer _serializer;
        private readonly string _keyPrefix;

        public RedisL2Cache(
            IConnectionMultiplexer redis,
            ICacheSerializer serializer,
            string keyPrefix = "cacheshield:")
        {
            ArgumentNullException.ThrowIfNull(redis);
            ArgumentNullException.ThrowIfNull(serializer);

            _redis = redis;
            _serializer = serializer;
            _keyPrefix = keyPrefix ?? string.Empty;
        }

        public async Task<(bool Found, T? Value)> TryGetAsync<T>(
            string key,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            cancellationToken.ThrowIfCancellationRequested();

            var db = _redis.GetDatabase();
            var redisValue = await db.StringGetAsync(BuildKey(key)).ConfigureAwait(false);

            if (!redisValue.HasValue)
            {
                return (false, default);
            }

            byte[]? bytes = redisValue;
            if (bytes is null)
            {
                return (false, default);
            }

            var deserialized = _serializer.Deserialize<T>(bytes);
            return (true, deserialized);
        }

        public async Task SetAsync<T>(
            string key,
            T value,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            cancellationToken.ThrowIfCancellationRequested();

            var db = _redis.GetDatabase();
            var bytes = _serializer.Serialize(value);

            await db.StringSetAsync(BuildKey(key), bytes, ttl).ConfigureAwait(false);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            cancellationToken.ThrowIfCancellationRequested();

            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(BuildKey(key)).ConfigureAwait(false);
        }

        private string BuildKey(string key) => string.Concat(_keyPrefix, key);
    }
}
