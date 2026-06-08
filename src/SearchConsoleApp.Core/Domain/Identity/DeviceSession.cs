using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Identity;

/// <summary>
/// Aktif login oturumu. Bir cihazdan bir login = bir DeviceSession.
/// RefreshToken ile 1-to-1 ilişkilidir (RefreshTokenHash'le bağlanır).
///
/// Logout, expired, admin-revoked durumlarında RevokedUtc set edilir.
/// IsActive computed property bunu hesaplar.
///
/// Audit + güvenlik için en kritik tablodur — "kim, ne zaman, nereden, hangi
/// cihazdan, hangi audience'a girdi" sorusunun cevabı burada.
/// </summary>
public partial class DeviceSession : BaseEntity
{
    public long CustomerId { get; set; }
    public long DeviceId { get; set; }

    /// <summary>Hangi audience'a (web/admin) girdi.</summary>
    public string Audience { get; set; } = "web";

    /// <summary>İlişkili refresh token'ın SHA-256 hash'i (RefreshToken.TokenHash ile aynı).</summary>
    public string? RefreshTokenHash { get; set; }

    public string? IpAddress { get; set; }

    /// <summary>GeoIP lookup ile doldurulur. ISO country code (TR, US, DE).</summary>
    public string? IpCountry { get; set; }

    /// <summary>Şehir bilgisi (opsiyonel, GeoIP provider'a bağlı).</summary>
    public string? IpCity { get; set; }

    public string? UserAgent { get; set; }

    public DateTime StartedUtc { get; set; }
    public DateTime LastActivityUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }

    /// <summary>'user' | 'admin' | 'expired' | 'security' | 'rotation' | null</summary>
    public string? RevokedReason { get; set; }

    /// <summary>'user' ise: kim revoke etti. 'admin' ise: hangi admin?</summary>
    public long? RevokedByCustomerId { get; set; }

    public bool IsActive => RevokedUtc == null;
}
