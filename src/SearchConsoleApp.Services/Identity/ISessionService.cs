using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Domain.Identity;

namespace SearchConsoleApp.Services.Identity;

public interface ISessionService
{
    /// <summary>
    /// Login sırasında yeni session oluştur. AuthService bunu çağırır.
    /// RefreshToken DB'ye yazıldıktan sonra hash'i burada bağlanır.
    /// </summary>
    Task<DeviceSession> StartAsync(long customerId, long deviceId, Audience audience,
                                    string refreshTokenHash, string? ip, string? userAgent);

    /// <summary>Aktif session — RefreshToken kullanıldıkça LastActivityUtc güncellenir.</summary>
    Task UpdateActivityAsync(string refreshTokenHash);

    /// <summary>Logout veya refresh rotation. RevokedReason setlenir.</summary>
    Task RevokeAsync(long sessionId, string reason, long? revokedByCustomerId = null);

    /// <summary>Refresh token hash'inden session bul ve revoke et (logout endpoint için).</summary>
    Task RevokeByRefreshTokenAsync(string refreshTokenHash, string reason);

    /// <summary>Bir kullanıcının diğer tüm aktif session'larını revoke et ("şüpheli aktivite").</summary>
    Task RevokeAllExceptAsync(long customerId, long exceptSessionId, string reason);

    /// <summary>Kullanıcının tüm aktif oturumları.</summary>
    Task<IList<DeviceSession>> GetActiveSessionsAsync(long customerId);

    /// <summary>Geçmiş dahil tüm oturumlar (admin için).</summary>
    Task<IList<DeviceSession>> GetAllSessionsAsync(long customerId, int take = 100);
}
