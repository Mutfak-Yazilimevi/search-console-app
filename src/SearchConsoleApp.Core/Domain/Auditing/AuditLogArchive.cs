namespace SearchConsoleApp.Core.Domain.Auditing;

/// <summary>
/// AuditLog'un arşivlenmiş hali. Aktif sorgu yükünü azaltmak için eski
/// kayıtlar buraya taşınır.
///
/// Aynı schema (AuditLog ile birebir), ayrı tablo.
/// - Aktif index'ler hafif kalır (hot data)
/// - Eski sorgular ayrı tabloya gider (cold data)
/// - Çok daha agresif retention uygulanabilir (5+ yıl)
///
/// Production önerisi: bu tabloyu farklı bir DB'ye veya read-replica'ya
/// taşımak (partition / sharding). Kod aynı kalır, ConnectionString değişir.
/// </summary>
public partial class AuditLogArchive
{
    public long Id { get; set; }
    public long OriginalId { get; set; }   // AuditLog.Id (lookup için)

    public DateTime Timestamp { get; set; }
    public string Audience { get; set; } = "unknown";

    public long? ActorCustomerId { get; set; }
    public string? ActorEmail { get; set; }
    public string? ActorIp { get; set; }
    public string? ActorUserAgent { get; set; }
    public long? ActorDeviceId { get; set; }
    public long? ActorSessionId { get; set; }

    public string Action { get; set; } = "";
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    public Guid? TargetEntityId { get; set; }

    public string? ChangesJson { get; set; }
    public string? MetadataJson { get; set; }

    public string Outcome { get; set; } = "success";
    public string? FailureReason { get; set; }

    public string? CorrelationId { get; set; }
    public long? TenantId { get; set; }

    public DateTime ArchivedOnUtc { get; set; }
}
