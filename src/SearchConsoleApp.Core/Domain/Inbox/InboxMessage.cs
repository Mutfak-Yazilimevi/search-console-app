using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Inbox;

/// <summary>
/// Inbox pattern — bize gelen webhook'ları/event'leri idempotent işlemek için.
///
/// Senaryo: Stripe webhook gönderiyor. Bizden 200 alamazsa retry yapar. Bu
/// retry sırasında aynı event iki kez işlenmemeli (örn. aynı sipariş iki kez
/// teslim edilmiş işaretlenmemeli).
///
/// İşleyiş:
/// 1. Webhook controller `ExternalEventId` (provider-specific) + `Source`
///    ile bu tabloya INSERT dener
/// 2. UniqueIndex(Source, ExternalEventId) — duplicate'i DB reddeder
/// 3. Duplicate: zaten işlenmiş, 200 dön (idempotent)
/// 4. Yeni: processing'e geç, business logic çalıştır, ProcessedUtc set et
///
/// Outbox'tan farkı: outbox bizim DIŞARI gönderdiklerimiz, inbox bize GELENLER.
/// İki tablo benzer ama amaçları zıt — birleştirmek yanıltıcı olur.
/// </summary>
public partial class InboxMessage : BaseEntity
{
    /// <summary>Provider/kaynak: "stripe", "github-webhook", "twilio", vb.</summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Provider'ın o event için verdiği unique ID.
    /// Stripe: `event.id` (evt_xxx)
    /// GitHub: `X-GitHub-Delivery` header
    /// </summary>
    public string ExternalEventId { get; set; } = "";

    /// <summary>Event tipi — handler routing için.</summary>
    public string EventType { get; set; } = "";

    /// <summary>Raw payload — debug + retry için saklı.</summary>
    public string Payload { get; set; } = "";

    public DateTime ReceivedUtc { get; set; }

    /// <summary>'received' | 'processed' | 'failed'</summary>
    public string Status { get; set; } = "received";

    public DateTime? ProcessedUtc { get; set; }
    public string? Error { get; set; }
}
