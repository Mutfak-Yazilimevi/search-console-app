using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Caching;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Core.RequestScope;

namespace SearchConsoleApp.Services.Caching;

/// <summary>
/// ICacheKeyFactory somut implementasyonu. IRequestScope'tan audience/tenant
/// okur, otomatik prefix'ler.
///
/// Lifetime: Scoped — IRequestScope'a bağlı.
/// </summary>
public class CacheKeyFactory : ICacheKeyFactory, IScopedService
{
    private const string AppPrefix = "SearchConsoleApp";

    private static readonly Audience[] AllAudiences =
    [
        Audience.Public,
        Audience.Web,
        Audience.Admin,
    ];

    private readonly IRequestScope _scope;
    public CacheKeyFactory(IRequestScope scope) => _scope = scope;

    public CacheKey For<TEntity>(string operation, params object[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        var entity = EntitySlug<TEntity>();
        var audience = _scope.Audience.ToSlug();
        var tenant = _scope.TenantId is long tid ? $"tenant{tid}." : "";

        var keyPrefix = $"{AppPrefix}.{audience}.{tenant}{entity}";
        var fullKey = args.Length == 0
            ? $"{keyPrefix}.{operation}"
            : $"{keyPrefix}.{operation}.{string.Join('.', args)}";

        // Invalidation hiyerarşisi:
        // - Bu audience+entity'ye özel prefix
        // - Tüm audience'ları kapsayan entity prefix
        var prefixes = new[]
        {
            $"{keyPrefix}.",
            $"{AppPrefix}.*.{tenant}{entity}.",   // wildcard — invalidator için marker
        };

        return new CacheKey(fullKey, TimeSpan.FromMinutes(15), prefixes);
    }

    public string PrefixFor<TEntity>()
    {
        var entity = EntitySlug<TEntity>();
        var audience = _scope.Audience.ToSlug();
        var tenant = _scope.TenantId is long tid ? $"tenant{tid}." : "";
        return $"{AppPrefix}.{audience}.{tenant}{entity}.";
    }

    public IReadOnlyList<string> AllAudiencePrefixesFor<TEntity>()
    {
        var entity = EntitySlug<TEntity>();
        var tenant = _scope.TenantId is long tid ? $"tenant{tid}." : "";
        return AllAudiences
            .Select(a => $"{AppPrefix}.{a.ToSlug()}.{tenant}{entity}.")
            .ToList();
    }

    private static string EntitySlug<TEntity>()
    {
        var name = typeof(TEntity).Name;
        // CamelCase → lower. Customer → customer, RefreshToken → refreshtoken
        return name.ToLowerInvariant();
    }
}
