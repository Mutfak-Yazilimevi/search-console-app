using SearchConsoleApp.Core.Domain.Outbox;

namespace SearchConsoleApp.Services.Outbox;

/// <summary>
/// Outbox dispatcher'ın çağırdığı handler. MessageType'a göre seçilir.
///
/// Mevcut impl:
/// - `WebhookOutboxHandler` — MessageType "webhook.*" pattern'i ile başlayanları HTTP POST'la gönderir.
///
/// Gelecekte eklenebilecekler:
/// - `RabbitMqOutboxHandler` — MessageType "broker.*" RabbitMQ'ya publish
/// - `KafkaOutboxHandler` — MessageType "kafka.*" Kafka topic'e produce
///
/// Handler kayıt: marker (`IScopedService`) ile auto-DI. Dispatcher tüm
/// IOutboxMessageHandler'ları enumerate edip ilk CanHandle olanı seçer.
/// </summary>
public interface IOutboxMessageHandler
{
    /// <summary>Bu handler verilen MessageType'ı işleyebilir mi?</summary>
    bool CanHandle(string messageType);

    /// <summary>
    /// Mesajı gönder. Başarılıysa silently return. Hata fırlatırsa dispatcher
    /// retry mantığını çalıştırır.
    ///
    /// PermanentException fırlat → dispatcher dead-letter olarak işaretler,
    /// retry yapmaz (4xx HTTP gibi).
    /// </summary>
    Task SendAsync(OutboxMessage message, CancellationToken ct);
}

/// <summary>
/// Kalıcı hata — retry anlamsız (4xx, validation error). Dispatcher dead'e atar.
/// </summary>
public class OutboxPermanentException : Exception
{
    public OutboxPermanentException(string message) : base(message) { }
    public OutboxPermanentException(string message, Exception inner) : base(message, inner) { }
}
