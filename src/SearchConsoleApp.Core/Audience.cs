namespace SearchConsoleApp.Core;

/// <summary>
/// Tüm sistem boyunca taşınan audience tipi.
/// Backend audience controller'larından başlayıp service'lere, cache'e,
/// event'lere, log'lara kadar her cross-cutting concern'de bu enum kullanılır.
///
/// Frontend tarafında string olarak ('public' | 'web' | 'admin') geçer.
/// </summary>
public enum Audience
{
    /// <summary>Anonim ziyaretçi — JWT yok. /api/public/*</summary>
    Public = 0,

    /// <summary>Giriş yapmış üye. /api/web/*</summary>
    Web = 1,

    /// <summary>Sistem yöneticisi. /api/admin/*</summary>
    Admin = 2,

    /// <summary>Background job, scheduled task — HTTP isteği değil.</summary>
    Background = 99,
}

public static class AudienceExtensions
{
    /// <summary>Cache key, log, metric tag'lerinde kullanılan kısa isim.</summary>
    public static string ToSlug(this Audience audience) => audience switch
    {
        Audience.Public => "public",
        Audience.Web => "web",
        Audience.Admin => "admin",
        Audience.Background => "bg",
        _ => "unknown"
    };
}
