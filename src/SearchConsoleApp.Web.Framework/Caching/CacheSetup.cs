using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SearchConsoleApp.Web.Framework.Caching;

public static class CacheSetup
{
    /// <summary>
    /// .NET 9 HybridCache kurulumu. Config'e göre L2 olarak Redis ekler.
    ///
    /// appsettings.json:
    ///   "Cache": {
    ///     "Provider": "memory" | "redis",
    ///     "Redis": { "ConnectionString": "localhost:6379" }
    ///   }
    ///
    /// memory: L1 (in-memory) — dev için ideal, tek instance.
    /// redis:  L1 (memory) + L2 (Redis) — prod, multiple instance, cluster.
    /// </summary>
    public static IServiceCollection AddSearchConsoleAppCache(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Cache:Provider"]?.ToLowerInvariant() ?? "memory";

        if (provider == "redis")
        {
            var redisConn = config["Cache:Redis:ConnectionString"]
                ?? throw new InvalidOperationException("Cache:Redis:ConnectionString gerekli.");

            services.AddStackExchangeRedisCache(opt =>
            {
                opt.Configuration = redisConn;
                opt.InstanceName = "SearchConsoleApp:";
            });
        }

        // HybridCache — IDistributedCache varsa L2 olarak kullanır,
        // yoksa sadece L1 (memory).
        services.AddHybridCache(opt =>
        {
            opt.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(15),
                LocalCacheExpiration = TimeSpan.FromMinutes(15),
            };
        });

        return services;
    }
}
