namespace SearchConsoleApp.Core.Caching;

/// <summary>
/// Cache key objesi. Manuel string.Format yapmak YASAK — bu sınıfı kullan.
/// </summary>
public class CacheKey
{
    public string Key { get; }
    public TimeSpan? CacheTime { get; }
    public string[] Prefixes { get; }

    public CacheKey(string key, TimeSpan? cacheTime = null, params string[] prefixes)
    {
        Key = key;
        CacheTime = cacheTime;
        Prefixes = prefixes ?? Array.Empty<string>();
    }

    /// <summary>Parametre yerleştirilmiş yeni key üretir.</summary>
    public CacheKey Create(params object[] keyObjects)
    {
        var formatted = string.Format(Key, keyObjects);
        return new CacheKey(formatted, CacheTime, Prefixes);
    }
}

public interface IStaticCacheManager
{
    Task<T?> GetAsync<T>(CacheKey key, Func<Task<T>> acquire);
    Task RemoveAsync(CacheKey key);
    Task RemoveByPrefixAsync(string prefix);
    Task ClearAsync();
}
