using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Core.Interfaces
{
    public interface ICacheBackplane
    {
        Task PublishInvalidationAsync(string key, string originId, CancellationToken cancellationToken = default);
        void SubscribeInvalidation(Action<string, string> onKeyInvalidated);
    }
}
