using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Outbox;

/// <summary>
/// Transactional outbox: business action ile aynı DB transaction'da yazılır,
/// sonra OutboxDispatcherService asenkron olarak gönderir (webhook, message broker).
///
/// Neden inline değil? "At-least-once delivery" garantisi:
/// - Business commit edildiyse mesaj kesin gider (DB'de kayıt var)
/// - Network/external service hatası → retry
/// - Process crash → uygulama restart'ta kaldığı yerden devam
///
/// Idempotency: receiver tarafı IdempotencyKey'i (Id'yi) kullanarak aynı
/// mesajın iki kez işlenmesini engellemeli.
///
/// Bu "outbox-only" implementation — inbox tarafı (gelen webhook'ları
/// idempotent işleme) Customer tarafının sorumluluğu. Inbox gerekirse
/// ayrı entity + middleware.
/// </summary>
public partial class OutboxMessage : BaseEntity
{
    /// <summary>
    /// Mesaj tipi (routing/handler seçimi için). Örn: "webhook.stripe.charge.created"
    /// veya "broker.OrderCreated".
    /// </summary>
    public string MessageType { get; set; } = "";

    /// <summary>
    /// Hedef belirteci. Webhook için URL, broker için topic/queue ismi.
    /// Tip-bağımlı: dispatcher MessageType'a göre yorumlar.
    /// </summary>
    public string Target { get; set; } = "";

    /// <summary>JSON payload — değişmez. Receiver bunu parse eder.</summary>
    public string Payload { get; set; } = "";

    /// <summary>HTTP headers (webhook için) veya broker properties. JSON dict.</summary>
    public string? HeadersJson { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    /// <summary>İlk denenmeden önce bekle (rate limit hedefi vb.).</summary>
    public DateTime? AvailableAtUtc { get; set; }

    /// <summary>Kaç kere denendi.</summary>
    public int AttemptCount { get; set; }

    public DateTime? LastAttemptUtc { get; set; }

    /// <summary>'pending' | 'in_progress' | 'succeeded' | 'failed' | 'dead'</summary>
    public string Status { get; set; } = "pending";

    public DateTime? CompletedUtc { get; set; }

    /// <summary>Son hata mesajı (debug). Başarılı send'de null.</summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Audience'a göre filtreleme — outbox audience-aware (örn. admin event'leri
    /// sadece admin webhook'larına gider).
    /// </summary>
    public string Audience { get; set; } = "background";

    public long? TenantId { get; set; }
    public string? CorrelationId { get; set; }
}
