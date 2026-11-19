using CacheShield.Core.Interfaces;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Infrastructure.Redis
{
    public sealed class RedisPubSubBackplane : ICacheBackplane
    {
        private const char Separator = '|';
        private readonly IConnectionMultiplexer _redis;
        private readonly RedisChannel _channel;

        public RedisPubSubBackplane(
            IConnectionMultiplexer redis,
            string channelName = "cacheshield:invalidation")
        {
            ArgumentNullException.ThrowIfNull(redis);
            ArgumentException.ThrowIfNullOrWhiteSpace(channelName);

            _redis = redis;
            _channel = RedisChannel.Literal(channelName);
        }

        public async Task PublishInvalidationAsync(
            string key,
            string originId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentException.ThrowIfNullOrWhiteSpace(originId);
            cancellationToken.ThrowIfCancellationRequested();

            var subscriber = _redis.GetSubscriber();
            var payload = $"{originId}{Separator}{key}";

            await subscriber.PublishAsync(_channel, payload).ConfigureAwait(false);
        }

        public void SubscribeInvalidation(Action<string, string> onKeyInvalidated)
        {
            ArgumentNullException.ThrowIfNull(onKeyInvalidated);

            var subscriber = _redis.GetSubscriber();
            subscriber.Subscribe(_channel, (_, message) =>
            {
                if (!message.HasValue)
                {
                    return;
                }

                var raw = message.ToString();
                var separatorIndex = raw.IndexOf(Separator);
                if (separatorIndex <= 0 || separatorIndex == raw.Length - 1)
                {
                    return;
                }

                var originId = raw[..separatorIndex];
                var key = raw[(separatorIndex + 1)..];

                onKeyInvalidated(key, originId);
            });
        }
    }
}
