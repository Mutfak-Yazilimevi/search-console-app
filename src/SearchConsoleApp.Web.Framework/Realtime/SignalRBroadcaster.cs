using Microsoft.AspNetCore.SignalR;
using SearchConsoleApp.Core.Realtime;

namespace SearchConsoleApp.Web.Framework.Realtime;

/// <summary>
/// INotificationBroadcaster SignalR implementasyonu.
///
/// IHubContext<TBootstrapHub, TClient> ile strongly-typed client çağrıları.
/// Magic string olmadan: hub.Clients.Group(...).SessionRevoked(...)
/// </summary>
public class SignalRBroadcaster : INotificationBroadcaster
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hub;

    public SignalRBroadcaster(IHubContext<NotificationHub, INotificationClient> hub) => _hub = hub;

    public Task SessionRevokedAsync(long customerId, long sessionId, string reason)
    {
        return _hub.Clients.Group($"user-{customerId}")
            .SessionRevoked(new SessionRevokedEvent(sessionId, reason));
    }

    public Task AuditEventAsync(AuditEventBroadcast e)
    {
        return _hub.Clients.Group("admin").AuditEvent(new AuditEventDto(
            e.Id, e.Timestamp, e.Audience,
            e.ActorCustomerId, e.ActorEmail,
            e.Action, e.TargetType, e.TargetId,
            e.Outcome));
    }

    public Task NotifyUserAsync(long customerId, string title, string message, string severity = "info")
    {
        return _hub.Clients.Group($"user-{customerId}")
            .UserNotification(new UserNotificationDto(title, message, severity));
    }
}

/// <summary>
/// SignalR yoksa (config'te disabled veya test'lerde) bu fallback devreye girer.
/// Service'ler crash etmez, sadece broadcast olmaz.
/// </summary>
public class NoOpBroadcaster : INotificationBroadcaster
{
    public Task SessionRevokedAsync(long customerId, long sessionId, string reason) => Task.CompletedTask;
    public Task AuditEventAsync(AuditEventBroadcast e) => Task.CompletedTask;
    public Task NotifyUserAsync(long customerId, string title, string message, string severity = "info") => Task.CompletedTask;
}
