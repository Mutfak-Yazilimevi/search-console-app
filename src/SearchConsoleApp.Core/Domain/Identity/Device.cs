using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Identity;

/// <summary>
/// Bir kullanıcının kalıcı cihaz kimliği. Aynı kullanıcı farklı cihazlardan
/// (telefon, laptop, iş bilgisayarı) ayrı kayıt tutar.
///
/// Fingerprint: UserAgent + Platform + Timezone + Screen + ek özelliklerin
/// SHA-256 hash'i. Cihazın "kim olduğunu" anlamak için kullanılır — kalıcı
/// olmasa da iyi bir yaklaşım. Cookie/localStorage temizlenince fingerprint
/// aynı çıkar (UA + ekran çözünürlüğü değişmediği sürece).
///
/// Trusted: 2FA atlanabilir (kullanıcı "bu cihaza güveniyorum" derse).
/// </summary>
public partial class Device : BaseEntity, ISoftDeletable
{
    public long CustomerId { get; set; }

    /// <summary>Cihazın benzersiz parmak izi (SHA-256 hex).</summary>
    public string Fingerprint { get; set; } = "";

    /// <summary>Kullanıcı tarafından atanabilir görünür ad. Ör: "Ali'nin iPhone".</summary>
    public string? Name { get; set; }

    /// <summary>Cihazın türü: 'web' | 'mobile-ios' | 'mobile-android' | 'desktop'.</summary>
    public string DeviceType { get; set; } = "web";

    /// <summary>İlk görüldüğü UA — değişmez.</summary>
    public string? FirstUserAgent { get; set; }

    /// <summary>Kullanıcı bu cihaza güveniyor mu? 2FA atlanabilir.</summary>
    public bool Trusted { get; set; }

    /// <summary>Bu cihazda biometric/passkey kayıtlı mı?</summary>
    public bool BiometricEnabled { get; set; }

    public DateTime FirstSeenUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }

    public bool Deleted { get; set; }
}
