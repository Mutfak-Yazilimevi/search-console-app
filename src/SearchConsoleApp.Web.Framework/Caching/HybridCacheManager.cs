using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Hybrid;
using SearchConsoleApp.Core.Caching;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Web.Framework.Caching;

/// <summary>
/// .NET 9 HybridCache wrapper'ı. Dev'de memory-only, prod'da Redis L2 + memory L1.
///
/// HybridCache native olarak prefix-based invalidation desteklemiyor —
/// `RemoveByPrefixAsync` için key listesini ayrıca takip ediyoruz.
/// Bu küçük bir veritabanı değil, sadece "hangi key hangi prefix'lere ait"
/// memory map'i. Tema/dil gibi nadir invalidation'larda yeterli.
/// </summary>
public class HybridCacheManager : IStaticCacheManager, ISingletonService
{
    private readonly HybridCache _cache;

    // prefix → keys map (prefix invalidation için)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _prefixIndex = new();

    public HybridCacheManager(HybridCache cache) => _cache = cache;

    public async Task<T?> GetAsync<T>(CacheKey key, Func<Task<T>> acquire)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(acquire);

        TrackKey(key);

        var options = new HybridCacheEntryOptions
        {
            Expiration = key.CacheTime ?? TimeSpan.FromMinutes(15),
            LocalCacheExpiration = key.CacheTime ?? TimeSpan.FromMinutes(15),
        };

        // Tag-based eviction için CacheKey.Prefixes'ı tag olarak kullan
        return await _cache.GetOrCreateAsync(
            key.Key,
            async _ => await acquire(),
            options,
            tags: key.Prefixes);
    }

    public async Task RemoveAsync(CacheKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        await _cache.RemoveAsync(key.Key);
        UntrackKey(key);
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return;

        // HybridCache tag-based invalidation (.NET 9.0)
        await _cache.RemoveByTagAsync(prefix);

        // Tracking index'ini de temizle
        _prefixIndex.TryRemove(prefix, out _);
    }

    public async Task ClearAsync()
    {
        // HybridCache'in built-in clear'i yok — tüm tag'leri tek tek invalidate et
        var prefixes = _prefixIndex.Keys.ToList();
        foreach (var prefix in prefixes)
            await _cache.RemoveByTagAsync(prefix);
        _prefixIndex.Clear();
    }

    private void TrackKey(CacheKey key)
    {
        foreach (var prefix in key.Prefixes)
        {
            var bucket = _prefixIndex.GetOrAdd(prefix, _ => new ConcurrentDictionary<string, byte>());
            bucket.TryAdd(key.Key, 0);
        }
    }

    private void UntrackKey(CacheKey key)
    {
        foreach (var prefix in key.Prefixes)
        {
            if (_prefixIndex.TryGetValue(prefix, out var bucket))
                bucket.TryRemove(key.Key, out _);
        }
    }
}
