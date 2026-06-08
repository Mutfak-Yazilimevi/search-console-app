using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.RequestScope;

namespace SearchConsoleApp.Web.Framework.RateLimiting;

public static class RateLimitingSetup
{
    public const string PublicPolicy = "public-rate";
    public const string WebPolicy = "web-rate";
    public const string AdminPolicy = "admin-rate";
    public const string AuthPolicy = "auth-rate";       // Login/register endpoint'leri için ekstra katı
    public const string AuditPolicy = "audit-rate";     // SEO tarama başlatma — abuse koruması

    /// <summary>
    /// Audience başına farklı rate limit.
    ///
    /// - Public:  IP başına 60/dakika (anonim, agresif scraping koruması)
    /// - Web:     Customer başına 300/dakika (üye normalde fazla istek atmaz)
    /// - Admin:   Customer başına 600/dakika (admin paneli yoğun olabilir)
    /// - Auth:    IP+endpoint başına 10/dakika (brute force koruması)
    ///
    /// Bu limitler config'ten override edilebilir:
    ///   "RateLimit:Public:PermitLimit": 60
    ///   "RateLimit:Public:WindowSeconds": 60
    ///
    /// Limit aşıldığında 429 Too Many Requests döner + Retry-After header.
    /// </summary>
    public static IServiceCollection AddSearchConsoleAppRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        services.AddRateLimiter(opt =>
        {
            opt.RejectionStatusCode = 429;

            // Public — IP-based
            opt.AddPolicy(PublicPolicy, context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = config.GetValue("RateLimit:Public:PermitLimit", 60),
                        Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:Public:WindowSeconds", 60)),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Web — Customer-based (authenticated)
            opt.AddPolicy(WebPolicy, context =>
            {
                var scope = context.RequestServices.GetRequiredService<IRequestScope>();
                var partition = scope.CustomerId?.ToString()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anon";
                return RateLimitPartition.GetFixedWindowLimiter(partition, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = config.GetValue("RateLimit:Web:PermitLimit", 300),
                        Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:Web:WindowSeconds", 60)),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Admin — Customer-based, daha cömert
            opt.AddPolicy(AdminPolicy, context =>
            {
                var scope = context.RequestServices.GetRequiredService<IRequestScope>();
                var partition = scope.CustomerId?.ToString() ?? "anon";
                return RateLimitPartition.GetFixedWindowLimiter(partition, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = config.GetValue("RateLimit:Admin:PermitLimit", 600),
                        Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:Admin:WindowSeconds", 60)),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Auth — IP+endpoint based, brute force koruması
            // (login, register, password reset gibi hassas endpoint'ler)
            opt.AddPolicy(AuthPolicy, context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var endpoint = context.Request.Path.ToString();
                var key = $"{ip}:{endpoint}";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = config.GetValue("RateLimit:Auth:PermitLimit", 10),
                        Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:Auth:WindowSeconds", 60)),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Audit start — IP veya customer başına katı limit
            opt.AddPolicy(AuditPolicy, context =>
            {
                var scope = context.RequestServices.GetRequiredService<Core.RequestScope.IRequestScope>();
                var partition = scope.CustomerId?.ToString()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anon";
                return RateLimitPartition.GetFixedWindowLimiter($"audit:{partition}", _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = config.GetValue("RateLimit:Audit:PermitLimit", 10),
                        Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:Audit:WindowSeconds", 3600)),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });

            // Global on-rejected handler — 429 + Retry-After
            opt.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Rate limit aşıldı. Lütfen daha sonra tekrar deneyin."
                }, ct);
            };
        });

        return services;
    }
}
