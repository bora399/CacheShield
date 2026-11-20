using CacheShield.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Application.Abstractions
{
    public interface ICacheShield
    {
        ValueTask<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            CacheEntryOptions? options = null,
            CancellationToken cancellationToken = default);

        Task SetAsync<T>(
            string key,
            T value,
            CacheEntryOptions? options = null,
            CancellationToken cancellationToken = default);

        Task RemoveAsync(
            string key,
            CancellationToken cancellationToken = default);
    }
}
