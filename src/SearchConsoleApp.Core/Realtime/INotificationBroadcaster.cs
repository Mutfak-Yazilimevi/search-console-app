namespace SearchConsoleApp.Core.Realtime;

/// <summary>
/// Service katmanından SignalR hub'a soyut köprü.
/// Core'da tanımlı çünkü Services bunu çağırıyor (Data→Core←Services bağımlılık yönü).
/// İmplementasyon Web.Framework'te.
///
/// SignalR olmadan da çalışır (no-op fallback) — service'ler bağımlılık kırılmadan
/// çalışır, sadece broadcast etmez.
/// </summary>
public interface INotificationBroadcaster
{
    /// <summary>Bir kullanıcının session'ı revoke edildi — açık client'ları bilgilendir.</summary>
    Task SessionRevokedAsync(long customerId, long sessionId, string reason);

    /// <summary>Admin paneline canlı audit event yolla.</summary>
    Task AuditEventAsync(AuditEventBroadcast e);

    /// <summary>Belirli kullanıcıya genel bildirim (toast, banner).</summary>
    Task NotifyUserAsync(long customerId, string title, string message, string severity = "info");
}

public record AuditEventBroadcast(
    long Id, DateTime Timestamp, string Audience,
    long? ActorCustomerId, string? ActorEmail,
    string Action, string? TargetType, long? TargetId,
    string Outcome);
