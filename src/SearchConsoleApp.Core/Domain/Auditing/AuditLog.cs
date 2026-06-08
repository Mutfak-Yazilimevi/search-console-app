using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Auditing;

/// <summary>
/// Business audit log — kalıcı, sorgulanabilir, hukuki kayıt.
///
/// Serilog'tan farkı:
/// - Serilog operational (debug, perf, error) — Console/file/Elastic akışı,
///   genelde 30 gün retention
/// - AuditLog business event'leri — DB'de kalıcı (yıllarca), kullanıcıya
///   gösterilebilir, hukuki gereklilikler için (GDPR, KVKK)
///
/// Doldurma stratejisi:
/// - Otomatik: tüm entity insert/update/delete event'leri (AuditEventConsumer)
/// - Manuel: [Audit("action.name")] attribute'lu controller action'lar (AuditFilter)
/// - Programatik: IAuditService.LogAsync(...) doğrudan service'lerden
///
/// İndexler: ActorCustomerId, TargetType+TargetId, Timestamp, Action.
/// Sorgu örnekleri:
/// - "Şu kullanıcının son 30 gün aktivitesi"
/// - "Şu entity'ye yapılan tüm değişiklikler"
/// - "Belirli IP'den gelen başarısız login denemeleri"
/// </summary>
public partial class AuditLog : BaseEntity
{
    /// <summary>Olayın tam zamanı (CreatedOnUtc duplicate olmasın diye ayrı).</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Hangi audience'tan geldi: 'public' | 'web' | 'admin' | 'bg'</summary>
    public string Audience { get; set; } = "unknown";

    // === Actor (kim yaptı) ===

    public long? ActorCustomerId { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorIp { get; set; }
    public string? ActorUserAgent { get; set; }
    public long? ActorDeviceId { get; set; }
    public long? ActorSessionId { get; set; }

    // === Action (ne yaptı) ===

    /// <summary>
    /// Dotted notation: "{entity}.{verb}" veya "auth.login", "auth.failed".
    /// Ör: "customer.update", "customer.delete", "auth.login", "theme.create"
    /// </summary>
    public string Action { get; set; } = "";

    /// <summary>Etkilenen entity tipi (Customer, Theme, vb.). Auth event'lerinde null.</summary>
    public string? TargetType { get; set; }

    /// <summary>Etkilenen entity'nin internal Id'si.</summary>
    public long? TargetId { get; set; }

    /// <summary>Etkilenen entity'nin EntityId'si (public Guid).</summary>
    public Guid? TargetEntityId { get; set; }

    // === Detail (opsiyonel) ===

    /// <summary>
    /// Eski/yeni değer snapshot'ı, JSON. Format:
    /// { "field": { "old": "...", "new": "..." } }
    /// Hassas alanlar (PasswordHash) filter'lanır — log'a girmez.
    /// </summary>
    public string? ChangesJson { get; set; }

    /// <summary>İlave metadata, request payload özeti vb. JSON.</summary>
    public string? MetadataJson { get; set; }

    /// <summary>'success' | 'failure' (varsayılan success).</summary>
    public string Outcome { get; set; } = "success";

    /// <summary>Failure ise sebep ("invalid_credentials", "permission_denied", vb.).</summary>
    public string? FailureReason { get; set; }

    /// <summary>Trace/correlation ID — Serilog log'ları ile cross-ref için.</summary>
    public string? CorrelationId { get; set; }

    // === Tenant (multi-tenant aktifse) ===

    public long? TenantId { get; set; }
}
