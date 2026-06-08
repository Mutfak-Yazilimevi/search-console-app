using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Customers;

/// <summary>
/// ÖRNEK entity. Sadece property — method/validation YOK.
/// `ISoftDeletable` implement ederek global query filter'a dahil olur.
/// Auth subject olarak da kullanılır (JWT sub claim = Customer.EntityId).
/// </summary>
public partial class Customer : BaseEntity, ISoftDeletable
{
    public string Email { get; set; } = "";
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool Active { get; set; }
    public bool Deleted { get; set; }      // ISoftDeletable

    /// <summary>Salt + Hash kombinasyonu, PBKDF2 ile. Null = login kapalı.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Email doğrulanmış mı?</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>Virgül ile ayrılmış roller, ör. "user,admin"</summary>
    public string Roles { get; set; } = "user";

    // === 2FA / MFA ===

    /// <summary>2FA aktif mi? Default false.</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>TOTP shared secret (Base32 encoded). Setup sonrası set edilir.</summary>
    public string? TotpSecret { get; set; }

    /// <summary>Backup recovery code'ları, virgülle ayrılmış SHA-256 hash'ler.</summary>
    public string? RecoveryCodesHashes { get; set; }

    /// <summary>
    /// Kullanıcının dil tercihi (ISO 639-1: "en", "tr", "de").
    /// Null ise Accept-Language header kullanılır (request bazlı).
    /// Set edilmişse bu, header'ı override eder — backend bildirimleri (email)
    /// için kalıcı tercih.
    /// </summary>
    public string? Language { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
}
