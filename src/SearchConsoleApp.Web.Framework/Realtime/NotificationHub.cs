using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SearchConsoleApp.Core.RequestScope;

namespace SearchConsoleApp.Web.Framework.Realtime;

/// <summary>
/// Real-time notification hub.
///
/// İki audience:
/// - `user-{customerId}` grup: o kullanıcıya yönelik (session revoke, push)
/// - `admin` grup: tüm admin'ler (canlı audit feed)
///
/// Connection lifecycle:
/// - OnConnected: JWT'den customerId çek, "user-{id}" grubuna ekle.
///   Admin rolündeyse ayrıca "admin" grubuna ekle.
/// - OnDisconnected: gruplar otomatik temizlenir.
///
/// Kullanım (server-side):
///   await _hub.Clients.Group($"user-{customerId}").SendAsync("SessionRevoked", sessionId);
///   await _hub.Clients.Group("admin").SendAsync("AuditEvent", entry);
///
/// Client-side: SignalR JS client (Angular) / @microsoft/signalr (RN).
/// </summary>
[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
    public override async Task OnConnectedAsync()
    {
        var customerId = Context.User?.FindFirst("uid")?.Value;
        if (!string.IsNullOrEmpty(customerId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{customerId}");
        }

        if (Context.User?.IsInRole("admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admin");
        }

        await base.OnConnectedAsync();
    }
}

/// <summary>
/// Strongly-typed hub client — server'dan client'a çağrılabilecek metodlar.
/// Bu interface'i implement etmek ZORUNDA değil — sadece sinyalleşme.
/// Magic string yerine method ismi compile-time kontrol.
/// </summary>
public interface INotificationClient
{
    /// <summary>Bir session revoke edildi — client logout veya tab kapatmalı.</summary>
    Task SessionRevoked(SessionRevokedEvent e);

    /// <summary>Yeni audit kaydı — admin canlı feed için.</summary>
    Task AuditEvent(AuditEventDto e);

    /// <summary>Genel kullanıcı bildirimi (uyarı, mesaj).</summary>
    Task UserNotification(UserNotificationDto e);
}

public record SessionRevokedEvent(long SessionId, string Reason);

public record AuditEventDto(
    long Id, DateTime Timestamp, string Audience,
    long? ActorCustomerId, string? ActorEmail,
    string Action, string? TargetType, long? TargetId,
    string Outcome);

public record UserNotificationDto(string Title, string Message, string? Severity = "info");
