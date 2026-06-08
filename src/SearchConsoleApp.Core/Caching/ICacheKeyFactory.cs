namespace SearchConsoleApp.Core.Caching;

/// <summary>
/// Cache key üreticisi. Geliştirici elle prefix yazmaz.
///
/// Final key formatı:  SearchConsoleApp.{audience}.{tenant?}.{entity}.{operation}.{args}
/// Örnek:
///   _keys.For&lt;Customer&gt;("byid", 42)
///     → "SearchConsoleApp.web.customer.byid.42"  (audience=Web, tenant=null)
///     → "SearchConsoleApp.admin.tenant5.customer.byid.42"  (audience=Admin, tenant=5)
///
/// Audience scope sayesinde:
/// - Aynı entity, farklı projeksiyon (admin full vs web partial) çakışmaz
/// - Multi-tenant'ta cross-tenant sızıntı imkansız
/// - Invalidation seçici yapılabilir: tek audience veya hepsi
///
/// `Prefixes` array'i de otomatik dolar — RemoveByPrefixAsync için.
/// </summary>
public interface ICacheKeyFactory
{
    /// <summary>
    /// Entity tipinden tip-güvenli cache key. Operation, audience, tenant
    /// otomatik eklenir.
    /// </summary>
    CacheKey For<TEntity>(string operation, params object[] args);

    /// <summary>
    /// Bir audience için belirli entity'nin tüm cache'ini temsil eden prefix.
    /// Invalidation için: `await cache.RemoveByPrefixAsync(_keys.PrefixFor&lt;Customer&gt;())`.
    /// </summary>
    string PrefixFor<TEntity>();

    /// <summary>
    /// Tüm audience'lar için bir entity'nin prefix listesi.
    /// Update edilen entity'nin admin/web/public projeksiyonlarını birden
    /// invalidate etmek için.
    /// </summary>
    IReadOnlyList<string> AllAudiencePrefixesFor<TEntity>();
}
