using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SearchConsoleApp.Services.Identity;

namespace SearchConsoleApp.Web.Framework.Health;

public static class HealthChecksSetup
{
    /// <summary>
    /// /health endpoint'i için health check'leri kaydeder.
    ///
    /// - DB: SQL Server bağlantısı
    /// - Redis: cache provider redis ise
    /// - GeoIP: DB dosyası yüklü mü
    ///
    /// Response format:
    /// {
    ///   "status": "Healthy" | "Degraded" | "Unhealthy",
    ///   "checks": [
    ///     { "name": "db", "status": "Healthy", "description": "..." },
    ///     ...
    ///   ]
    /// }
    /// </summary>
    public static IServiceCollection AddSearchConsoleAppHealthChecks(this IServiceCollection services, IConfiguration config)
    {
        var checks = services.AddHealthChecks()
            // DB
            .AddSqlServer(
                connectionString: config.GetConnectionString("Default")!,
                name: "db",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready", "infra" });

        // Redis — sadece cache provider redis ise
        var cacheProvider = config["Cache:Provider"]?.ToLowerInvariant();
        if (cacheProvider == "redis")
        {
            var redisConn = config["Cache:Redis:ConnectionString"];
            if (!string.IsNullOrEmpty(redisConn))
            {
                checks.AddRedis(redisConn, name: "redis",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "ready", "infra" });
            }
        }

        // GeoIP — degraded only (sistemi durdurmaz)
        checks.AddCheck<GeoIpHealthCheck>(
            "geoip",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "feature" });

        checks.AddCheck<CrawlWorkerHealthCheck>(
            "crawl-worker",
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "ready", "audit" });

        return services;
    }
}

/// <summary>
/// GeoIP database yüklü mü kontrolü. Yoksa Degraded — sistem çalışır
/// ama IpCountry/City null kalır.
/// </summary>
public class GeoIpHealthCheck : IHealthCheck
{
    private readonly IGeoIpService _geoIp;

    public GeoIpHealthCheck(IGeoIpService geoIp) => _geoIp = geoIp;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        // GeoIpService null döndürüyorsa DB yüklenmemiş demektir
        // (NoOp gibi davranır). Loopback IP ile probe et — gerçek lookup yapmaz
        // ama "ready mi" kontrolünü yapar.
        var probe = _geoIp.Lookup("8.8.8.8");  // public DNS — başarısızsa DB yok
        return Task.FromResult(probe == null
            ? HealthCheckResult.Degraded("GeoIP database yüklenmemiş — IpCountry/IpCity null kalacak.")
            : HealthCheckResult.Healthy("GeoIP active"));
    }
}
