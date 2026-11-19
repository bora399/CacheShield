using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Core.Interfaces
{
    public interface IL2Cache
    {
        Task<(bool Found, T? Value)> TryGetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T Value, TimeSpan ttl, CancellationToken cancellationToken = default);
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    }
}
