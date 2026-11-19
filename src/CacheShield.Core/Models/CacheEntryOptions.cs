using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Core.Models
{
    public sealed record CacheEntryOptions
    {
        //l1 - ram
        public TimeSpan L1Expiration { get; init; } = TimeSpan.FromMinutes(1);
        //l2 - redis
        public TimeSpan L2Expiration { get; init; } = TimeSpan.FromMinutes(10);

        public static CacheEntryOptions Default => new();
    }
}
