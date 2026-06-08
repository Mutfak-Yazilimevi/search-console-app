using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Core.RequestScope;

namespace SearchConsoleApp.Web.Framework.Observability;

/// <summary>
/// Her request başında aktif OpenTelemetry Activity'e audience/tenant tag'i ekler.
/// Middleware olarak kayıt edilir (Program.cs).
///
/// Metric counter'larında da audience tag'i kullanmak için tek satırlık helper:
///   AudienceTagEnricher.AddTags(scope, KeyValuePair.Create("operation", "login"))
/// </summary>
public class AudienceTagEnricher : ISingletonService
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next, IRequestScope scope)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            activity.SetTag("audience", scope.Audience.ToSlug());
            if (scope.TenantId.HasValue) activity.SetTag("tenant_id", scope.TenantId.Value);
            if (scope.CustomerId.HasValue) activity.SetTag("customer_id", scope.CustomerId.Value);
        }
        return next(context);
    }

    /// <summary>Counter/histogram için audience tag'leri ile birlikte tags listesi üretir.</summary>
    public static KeyValuePair<string, object?>[] BuildTags(
        IRequestScope scope, params KeyValuePair<string, object?>[] extra)
    {
        var tags = new List<KeyValuePair<string, object?>>(extra.Length + 3)
        {
            new("audience", scope.Audience.ToSlug())
        };
        if (scope.TenantId.HasValue) tags.Add(new("tenant_id", scope.TenantId.Value));
        if (scope.CustomerId.HasValue) tags.Add(new("customer_id", scope.CustomerId.Value));
        tags.AddRange(extra);
        return tags.ToArray();
    }
}
