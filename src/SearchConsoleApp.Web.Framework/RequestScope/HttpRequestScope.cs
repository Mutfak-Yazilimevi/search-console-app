using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Core.RequestScope;

namespace SearchConsoleApp.Web.Framework.RequestScope;

/// <summary>
/// HTTP context'ten audience/tenant/customer çıkarır. Background job'larda
/// `RequestScopeMutator.BeginScope()` ile override edilebilir (AsyncLocal).
///
/// Audience tespiti: route prefix'inden — `/api/admin/*` → Admin.
/// </summary>
public class HttpRequestScope : IRequestScope, IRequestScopeMutator, IScopedService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly AsyncLocal<ScopeOverride?> _override = new();

    public HttpRequestScope(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Audience Audience
    {
        get
        {
            if (_override.Value != null) return _override.Value.Audience;
            return DetermineAudienceFromPath(_httpContextAccessor.HttpContext?.Request.Path);
        }
    }

    public long? TenantId
    {
        get
        {
            if (_override.Value != null) return _override.Value.TenantId;
            // Multi-tenancy aktif değilse null. Aktif olduğunda buraya tenant
            // resolution (subdomain/header/JWT claim) gelecek.
            return null;
        }
    }

    public long? CustomerId
    {
        get
        {
            if (_override.Value != null) return _override.Value.CustomerId;
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx?.User.Identity?.IsAuthenticated != true) return null;

            var uid = ctx.User.FindFirstValue("uid");
            return long.TryParse(uid, out var id) ? id : null;
        }
    }

    public Guid? CustomerEntityId
    {
        get
        {
            if (_override.Value != null) return null;  // background'da entity ID taşımıyoruz
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx?.User.Identity?.IsAuthenticated != true) return null;

            var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? ctx.User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public long? SessionId
    {
        get
        {
            if (_override.Value != null) return null;  // background'da session yok
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx?.User.Identity?.IsAuthenticated != true) return null;

            var sid = ctx.User.FindFirstValue("sid");
            return long.TryParse(sid, out var id) ? id : null;
        }
    }

    public string? CorrelationId
        => _httpContextAccessor.HttpContext?.TraceIdentifier;

    public IDisposable BeginScope(Audience audience, long? tenantId = null, long? customerId = null)
    {
        var previous = _override.Value;
        _override.Value = new ScopeOverride(audience, tenantId, customerId);
        return new DisposableScope(() => _override.Value = previous);
    }

    private static Audience DetermineAudienceFromPath(PathString? path)
    {
        if (path == null) return Audience.Background;
        var p = (path.Value.Value ?? string.Empty).AsSpan();
        if (p.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase)) return Audience.Admin;
        if (p.StartsWith("/api/web", StringComparison.OrdinalIgnoreCase)) return Audience.Web;
        if (p.StartsWith("/api/public", StringComparison.OrdinalIgnoreCase)) return Audience.Public;
        return Audience.Background;
    }

    private record ScopeOverride(Audience Audience, long? TenantId, long? CustomerId);

    private class DisposableScope : IDisposable
    {
        private readonly Action _onDispose;
        public DisposableScope(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
