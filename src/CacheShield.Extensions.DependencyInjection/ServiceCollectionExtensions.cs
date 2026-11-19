using CacheShield.Application.Abstractions;
using CacheShield.Application.Services;
using CacheShield.Core.Concurrency;
using CacheShield.Core.DataStructures;
using CacheShield.Core.Interfaces;
using CacheShield.Infrastructure.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheShield.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCacheShield(
            this IServiceCollection services,
            Action<CacheShieldOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            var options = new CacheShieldOptions();
            configure(options);
            options.Validate();

            services.TryAddSingleton<IClock, SystemClock>();
            services.TryAddSingleton<ICacheSerializer, SystemTextJsonSerializer>();
            services.TryAddSingleton<SingleflightLock>();

            services.TryAddSingleton<IL1Cache>(sp =>
                new LruCache(options.L1Capacity, sp.GetRequiredService<IClock>()));

            services.TryAddSingleton<IConnectionMultiplexer>(_ =>
            {
                var redisConfig = ConfigurationOptions.Parse(options.RedisConnectionString);
                redisConfig.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(redisConfig);
            });

            services.TryAddSingleton<IL2Cache>(sp =>
                new RedisL2Cache(
                    sp.GetRequiredService<IConnectionMultiplexer>(),
                    sp.GetRequiredService<ICacheSerializer>(),
                    options.KeyPrefix));

            if (options.EnableBackplane)
            {
                services.TryAddSingleton<ICacheBackplane>(sp =>
                    new RedisPubSubBackplane(
                        sp.GetRequiredService<IConnectionMultiplexer>(),
                        options.InvalidationChannel));
            }

            services.TryAddSingleton<ICacheShield>(sp =>
                new HybridCacheService(
                    sp.GetRequiredService<IL1Cache>(),
                    sp.GetRequiredService<IL2Cache>(),
                    sp.GetRequiredService<SingleflightLock>(),
                    sp.GetService<ICacheBackplane>()));

            return services;
        }
    }
}
