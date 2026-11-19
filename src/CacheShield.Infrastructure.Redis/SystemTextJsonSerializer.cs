using CacheShield.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CacheShield.Infrastructure.Redis
{
    public sealed class SystemTextJsonSerializer : ICacheSerializer
    {
        private readonly JsonSerializerOptions _options;

        public SystemTextJsonSerializer(JsonSerializerOptions? options = null)
        {
            _options = options ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public byte[] Serialize<T>(T value)
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, _options);
        }

        public T? Deserialize<T>(byte[] data)
        {
            if (data is null || data.Length == 0)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(data, _options);//redis utf-8 algıladıgı icin ve normalde json serializer utf-16 ürettiginden bunu yaptık.
        }
    }
}
