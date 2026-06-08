using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Security;

public partial class SecurityTokenService : ISecurityTokenService, IScopedService
{
    private readonly IRepository<SecurityToken> _repo;

    public SecurityTokenService(IRepository<SecurityToken> repo) => _repo = repo;

    public virtual async Task<string> IssueAsync(long customerId, string purpose, TimeSpan ttl, string? ip = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        // Aynı purpose için varolan aktif token'ları revoke et
        // (tek aktif token kuralı — kullanıcı "tekrar gönder" derse eskisi geçersiz)
        var now = DateTime.UtcNow;
        await _repo.Table
            .Where(t => t.CustomerId == customerId && t.Purpose == purpose && t.UsedUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedUtc, now));

        // 32 byte = 256 bit, URL-safe base64
        var bytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var token = new SecurityToken
        {
            CustomerId = customerId,
            TokenHash = HashToken(rawToken),
            Purpose = purpose,
            CreatedOnUtc = now,
            ExpiresOnUtc = now.Add(ttl),
            CreatedByIp = ip,
        };
        await _repo.InsertAsync(token);

        return rawToken;
    }

    public virtual async Task<long?> ConsumeAsync(string rawToken, string purpose)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var hash = HashToken(rawToken);

        var token = await _repo.Table
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose);

        if (token == null || !token.IsActive) return null;

        token.UsedUtc = DateTime.UtcNow;
        await _repo.UpdateAsync(token, publishEvent: false);

        return token.CustomerId;
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
