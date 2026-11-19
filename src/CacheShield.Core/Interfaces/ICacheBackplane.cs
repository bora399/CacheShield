using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Core.Interfaces
{
    public interface ICacheBackplane
    {
        Task PublishInvalidationAsync(string key, CancellationToken cancellationToken = default);//sunuculara iletim
        void SubscribeInvalidation(Action<string> onKeyInvalidated);//dinleme
    }
}
