using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Core.Interfaces
{
    public interface IL1Cache
    {
        bool TryGet<T>(string key, out T? value);
        void Set<T>(string key, T value, TimeSpan ttl);
        void Remove(string key);
        void Clear();//rame erişmek kısa sürdüğünden task kullanıp async awaitle uğraşmıyoruz.
    }
}
