namespace SearchConsoleApp.Services.Outbox;

/// <summary>
/// Business service'lerden outbox'a mesaj enqueue arayüzü.
///
/// Kullanım pattern (aynı transaction içinde):
///   var order = new Order { ... };
///   await _orderRepo.InsertAsync(order);
///   await _outbox.EnqueueAsync(new OutboxEnqueue {
///     MessageType = "webhook.order.created",
///     Target = "https://customer-webhook.example.com/orders",
///     Payload = JsonSerializer.Serialize(new { orderId = order.EntityId, ... }),
///   });
///
/// Outbox row InsertAsync ile DB'ye eklenir. EF Core'un DbContext aynı
/// SaveChanges'da business + outbox kayıtlarını birlikte commit eder.
/// Dispatcher arka planda mesajı işler.
/// </summary>
public interface IOutbox
{
    Task EnqueueAsync(OutboxEnqueue message);
}

/// <summary>Caller'ın doldurduğu enqueue payload.</summary>
public class OutboxEnqueue
{
    public string MessageType { get; set; } = "";
    public string Target { get; set; } = "";
    public string Payload { get; set; } = "";
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>İlk dene zamanı. Null = hemen.</summary>
    public DateTime? AvailableAtUtc { get; set; }
}
