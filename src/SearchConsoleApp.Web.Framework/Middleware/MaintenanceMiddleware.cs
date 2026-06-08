using Microsoft.AspNetCore.Http;
using SearchConsoleApp.Core.FeatureFlags;

namespace SearchConsoleApp.Web.Framework.Middleware;

/// <summary>
/// Maintenance mode middleware — `maintenance-mode` feature flag açıkken
/// /health endpoint'leri hariç tüm istekler 503 döner.
///
/// Deploy'dan tamamen bağımsız maintenance — config update yeterli (LaunchDarkly
/// gibi gerçek provider kullanılıyorsa anlık).
///
/// Admin'ler hariç tutulur (context'te role bilgisi varsa).
/// </summary>
public class MaintenanceMiddleware
{
    private readonly RequestDelegate _next;
    public MaintenanceMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IFeatureFlags flags)
    {
        var path = context.Request.Path.ToString();

        // Health endpoint'leri her zaman erişilebilir — k8s probe'ları için
        if (path.StartsWith("/health"))
        {
            await _next(context);
            return;
        }

        // Admin'lere izin ver (kapatma sonrası test/erişim için)
        var roles = context.User?.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList() ?? new List<string>();

        var evalContext = new EvaluationContext
        {
            Attributes = { ["roles"] = roles }
        };

        if (await flags.IsEnabledAsync(FeatureFlagKeys.MaintenanceMode, false, evalContext))
        {
            // Admin değilse 503
            if (!roles.Contains("admin"))
            {
                context.Response.StatusCode = 503;
                context.Response.Headers.RetryAfter = "300";  // 5dk
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = "maintenance_mode",
                    message = "Sistem geçici olarak bakımda. Lütfen birazdan tekrar deneyin."
                });
                return;
            }
        }

        await _next(context);
    }
}
