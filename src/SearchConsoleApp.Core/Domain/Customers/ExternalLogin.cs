using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Customers;

/// <summary>
/// Bir customer'ın bağlı external OAuth/OIDC provider'ları.
/// Aynı customer birden çok provider'a bağlanabilir (Google + GitHub + ...).
///
/// Unique key: (Provider, ProviderUserId) — provider'ın o user için verdiği
/// stable identifier. Email kullanmıyoruz çünkü email değişebilir.
///
/// AccessToken/RefreshToken DB'ye yazılmaz — sadece kimlik doğrulama için
/// kullanılır, sonra atılır. Provider'a sonradan çağrı yapmak gerekirse
/// (örn. Gmail API), ayrı entity/tablo ile token saklanır.
/// </summary>
public partial class ExternalLogin : BaseEntity
{
    public long CustomerId { get; set; }

    /// <summary>"google" | "microsoft" | "github" | "apple"</summary>
    public string Provider { get; set; } = "";

    /// <summary>Provider'ın o user için stable ID'si (Google: "sub" claim).</summary>
    public string ProviderUserId { get; set; } = "";

    /// <summary>Provider'dan gelen email (kayıt anında).</summary>
    public string? Email { get; set; }

    /// <summary>Provider'dan gelen display name.</summary>
    public string? DisplayName { get; set; }

    public DateTime LinkedOnUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
}
