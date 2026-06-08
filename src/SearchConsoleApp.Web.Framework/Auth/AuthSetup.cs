using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Services.Auth;

namespace SearchConsoleApp.Web.Framework.Auth;

public static class AuthSetup
{
    /// <summary>
    /// PreAuthTokenStore'u config'e göre kaydeder.
    ///
    /// Cache:Provider = "redis"     → DistributedPreAuthTokenStore
    /// Cache:Provider = "memory"    → InMemoryPreAuthTokenStore
    ///
    /// Multi-instance deployment'ta redis ZORUNLU — yoksa pod1'de oluşturulan
    /// preAuth token pod2'de consume edilemez ve 2FA login akışı kırılır.
    ///
    /// Marker pattern kullanılamaz çünkü iki impl de IPreAuthTokenStore —
    /// runtime seçim gerek.
    /// </summary>
    public static IServiceCollection AddSearchConsoleAppPreAuthStore(this IServiceCollection services, IConfiguration config)
    {
        var cacheProvider = config["Cache:Provider"]?.ToLowerInvariant() ?? "memory";

        if (cacheProvider == "redis")
        {
            // Redis backend — IDistributedCache zaten kayıtlı (CacheSetup)
            services.AddSingleton<IPreAuthTokenStore, DistributedPreAuthTokenStore>();
        }
        else
        {
            services.AddSingleton<IPreAuthTokenStore, InMemoryPreAuthTokenStore>();
        }

        return services;
    }
}
