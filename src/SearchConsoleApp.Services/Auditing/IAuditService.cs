using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Domain.Auditing;

namespace SearchConsoleApp.Services.Auditing;

/// <summary>
/// Audit log yazımı için tek nokta.
///
/// Üç doldurma yolu hepsi sonuçta buraya gelir:
/// 1. Otomatik (AuditEntityEventConsumer) — entity insert/update/delete event'leri
/// 2. Otomatik attribute (AuditFilter) — [Audit("action.name")] controller action'ları
/// 3. Manuel — service'lerden direkt: `_audit.LogAsync(new AuditEntry { ... })`
///
/// Tüm action'lar IRequestScope ve HttpContext'ten Actor/IP/Audience bilgisini
/// otomatik doldurur — caller sadece domain bilgisini verir.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Tek bir audit kaydı yaz. Actor ve context bilgileri (IRequestScope) otomatik doldurulur.
    /// </summary>
    Task LogAsync(AuditEntry entry);

    /// <summary>
    /// Kullanıcı/entity için audit log sorgulama.
    /// </summary>
    Task<IList<AuditLog>> QueryAsync(AuditQuery query);
}

/// <summary>Caller'ın doldurduğu kısım. Diğer alanlar service'te eklenir.</summary>
public class AuditEntry
{
    public string Action { get; set; } = "";
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    public Guid? TargetEntityId { get; set; }
    public string? ChangesJson { get; set; }
    public string? MetadataJson { get; set; }
    public string Outcome { get; set; } = "success";
    public string? FailureReason { get; set; }
}

public class AuditQuery
{
    public long? ActorCustomerId { get; set; }
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    public Audience? Audience { get; set; }
    public string? Action { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Take { get; set; } = 100;
    public int Skip { get; set; } = 0;
}
