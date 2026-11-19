using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Extensions.DependencyInjection
{
    public sealed class CacheShieldOptions
    {
        public int L1Capacity { get; set; } = 10_000;
        public string RedisConnectionString { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = "cacheshield:";
        public string InvalidationChannel { get; set; } = "cacheshield:invalidation";
        public bool EnableBackplane { get; set; } = true;

        internal void Validate()
        {
            if (L1Capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(L1Capacity),
                    "l1Capacity sıfırdan büyük olmalıdır.");
            }

            if (string.IsNullOrWhiteSpace(RedisConnectionString))
            {
                throw new ArgumentException(
                    "redisConnectionString boş bırakılamaz.",
                    nameof(RedisConnectionString));
            }
        }
    }
}
