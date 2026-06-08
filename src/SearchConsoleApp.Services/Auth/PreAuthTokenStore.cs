using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Distributed;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Auth;

/// <summary>
/// 2FA login akışında ilk adım (parola doğrulandı, 2FA bekleniyor) ile
/// ikinci adım (code doğrulandı) arasında köprü kuran geçici token.
///
/// İki impl:
/// - `InMemoryPreAuthTokenStore` — single-instance / dev / test
/// - `DistributedPreAuthTokenStore` — Redis / multi-instance production
///
/// Cache:Provider config'ine göre setup'ta hangisinin kayıt olacağı seçilir
/// (`AuthSetup.AddSearchConsoleAppPreAuthStore`).
///
/// Token formatı: 256-bit random base64url.
/// Bir kez consume edilince geçersiz olur (replay attack koruması).
/// </summary>
public interface IPreAuthTokenStore
{
    /// <summary>Customer için yeni preAuth token üret ve sakla.</summary>
    Task<string> CreateAsync(long customerId, TimeSpan ttl);

    /// <summary>Token'ı consume et — geçerliyse CustomerId döner ve token silinir.</summary>
    Task<long?> ConsumeAsync(string token);
}

public class InMemoryPreAuthTokenStore : IPreAuthTokenStore
{
    private record Entry(long CustomerId, DateTime ExpiresUtc);

    private readonly ConcurrentDictionary<string, Entry> _store = new();

    public Task<string> CreateAsync(long customerId, TimeSpan ttl)
    {
        var token = GenerateToken();
        _store[token] = new Entry(customerId, DateTime.UtcNow.Add(ttl));
        CleanupExpired();
        return Task.FromResult(token);
    }

    public Task<long?> ConsumeAsync(string token)
    {
        if (string.IsNullOrEmpty(token)) return Task.FromResult<long?>(null);
        if (!_store.TryRemove(token, out var entry)) return Task.FromResult<long?>(null);
        if (DateTime.UtcNow > entry.ExpiresUtc) return Task.FromResult<long?>(null);
        return Task.FromResult<long?>(entry.CustomerId);
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var (key, entry) in _store)
        {
            if (now > entry.ExpiresUtc) _store.TryRemove(key, out _);
        }
    }

    internal static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

/// <summary>
/// IDistributedCache (Redis) tabanlı impl. Multi-instance deployment için.
///
/// HybridCache veya IDistributedCache zaten configured (Cache:Provider=redis
/// ise). Bu impl onu kullanır — ayrı Redis connection açmaz.
///
/// Key formatı: `preauth:{token}` — namespace ayrımı için.
/// TTL Redis tarafında set edilir, manuel cleanup gerekmez.
/// </summary>
public class DistributedPreAuthTokenStore : IPreAuthTokenStore
{
    private const string KeyPrefix = "preauth:";

    private readonly IDistributedCache _cache;

    public DistributedPreAuthTokenStore(IDistributedCache cache) => _cache = cache;

    public async Task<string> CreateAsync(long customerId, TimeSpan ttl)
    {
        var token = InMemoryPreAuthTokenStore.GenerateToken();
        await _cache.SetStringAsync(
            KeyPrefix + token,
            customerId.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl });
        return token;
    }

    public async Task<long?> ConsumeAsync(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var key = KeyPrefix + token;

        var value = await _cache.GetStringAsync(key);
        if (string.IsNullOrEmpty(value)) return null;

        // Tek kullanım: hemen sil
        await _cache.RemoveAsync(key);

        return long.TryParse(value, out var customerId) ? customerId : null;
    }
}
