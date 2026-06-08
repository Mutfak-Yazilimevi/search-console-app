namespace SearchConsoleApp.Core;

/// <summary>
/// Tüm domain entity'leri için temel sınıf.
///
/// İki kimlik taşır:
/// - <see cref="Id"/>      → long, internal ilişkiler ve performans için (FK, index).
/// - <see cref="EntityId"/> → Guid, dış dünyaya açılan stabil kimlik (URL, API, mesaj kuyruğu).
///
/// İş kuralı veya method EKLENMEZ.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Internal numeric ID. Veritabanı PK ve FK'lerde kullanılır.</summary>
    public long Id { get; set; }

    /// <summary>
    /// Public-facing stabil kimlik. URL ve API'lerde Id yerine bunu kullan.
    /// Insert öncesi DB'ye yazılırken set edilir (varsayılan: Guid v7 sıralı GUID).
    /// </summary>
    public Guid EntityId { get; set; }
}

/// <summary>
/// Soft delete destekli entity'ler bu interface'i implement eder.
/// DbContext bunu OnModelCreating'de global query filter ile yakalayıp
/// `Deleted = true` olan kayıtları default sorgulardan dışlar.
/// </summary>
public interface ISoftDeletable
{
    bool Deleted { get; set; }
}
