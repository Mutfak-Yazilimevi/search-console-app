using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Customers;

/// <summary>
/// Refresh token kayıtları. Access token kısa ömürlü (60dk),
/// refresh token uzun ömürlü (30 gün) — kullanıcı her seferinde login olmasın.
///
/// Rotation paterni: refresh kullanılınca eskisi revoke edilir, yenisi üretilir.
/// </summary>
public partial class RefreshToken : BaseEntity
{
    public long CustomerId { get; set; }
    public string TokenHash { get; set; } = "";   // SHA-256 hash, raw token DB'de tutulmaz
    public DateTime CreatedOnUtc { get; set; }
    public DateTime ExpiresOnUtc { get; set; }
    public DateTime? RevokedOnUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }  // Rotation için
    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public bool IsActive => RevokedOnUtc == null && DateTime.UtcNow < ExpiresOnUtc;
}
