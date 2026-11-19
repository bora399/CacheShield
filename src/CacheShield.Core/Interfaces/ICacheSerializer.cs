using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Core.Interfaces
{
    public interface ICacheSerializer
    {
        byte[] Serialize<T>(T value);
        T? Deserialize<T>(byte[] data);
    }
}
