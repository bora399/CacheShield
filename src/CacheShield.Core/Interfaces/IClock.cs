using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Core.Interfaces
{
    public interface IClock
    {
        DateTimeOffset UtcNow { get; } //testler icin olusturuyoruz datetime.utcnow da kullanılabilir
    }
}
