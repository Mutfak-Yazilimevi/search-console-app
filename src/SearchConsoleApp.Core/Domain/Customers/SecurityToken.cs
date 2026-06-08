using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Customers;

/// <summary>
/// Tek kullanımlık, süreli güvenlik token'ı.
/// İki ana kullanım: email doğrulama ve password reset.
///
/// Token raw değeri client'a gider (email içinde link), DB'de sadece
/// SHA-256 hash'i tutulur — DB sızsa bile aktif token'lar kullanılamaz.
///
/// `Purpose` ile aynı tablo iki ayrı akış için kullanılır:
/// - "email_verification"
/// - "password_reset"
///
/// Tek kullanımlık: `UsedUtc` set edilince geçersizleşir.
/// </summary>
public partial class SecurityToken : BaseEntity
{
    public long CustomerId { get; set; }

    /// <summary>SHA-256 hash hex format. Raw token sadece email'de.</summary>
    public string TokenHash { get; set; } = "";

    /// <summary>"email_verification" | "password_reset"</summary>
    public string Purpose { get; set; } = "";

    public DateTime CreatedOnUtc { get; set; }
    public DateTime ExpiresOnUtc { get; set; }
    public DateTime? UsedUtc { get; set; }

    /// <summary>Token üretildiği IP — şüpheli olaylar için.</summary>
    public string? CreatedByIp { get; set; }

    public bool IsActive => UsedUtc == null && DateTime.UtcNow < ExpiresOnUtc;
}
