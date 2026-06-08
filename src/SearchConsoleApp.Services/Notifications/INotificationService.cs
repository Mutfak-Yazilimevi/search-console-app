using SearchConsoleApp.Core.Domain.Notifications;

namespace SearchConsoleApp.Services.Notifications;

public interface IDeviceTokenService
{
    Task RegisterAsync(long customerId, string token, string provider, string platform,
                       string? deviceName, string? appVersion);
    Task UnregisterAsync(long customerId, string token);
    Task<IList<DeviceToken>> GetByCustomerAsync(long customerId);
}

public interface INotificationService
{
    /// <summary>Tek kullanıcının tüm cihazlarına push gönder.</summary>
    Task<NotificationResult> SendToCustomerAsync(long customerId, PushMessage message);

    /// <summary>Birden fazla kullanıcıya batch push (segment, kampanya).</summary>
    Task<NotificationResult> SendToCustomersAsync(IEnumerable<long> customerIds, PushMessage message);

    /// <summary>Doğrudan token listesine — kullanıcı bağımsız (test, admin).</summary>
    Task<NotificationResult> SendToTokensAsync(IEnumerable<string> expoTokens, PushMessage message);
}

public record PushMessage(
    string Title,
    string Body,
    Dictionary<string, object>? Data = null,
    string? Sound = "default",
    int? Badge = null,
    string? ChannelId = "default");

public record NotificationResult(int Sent, int Failed, IReadOnlyList<string> InvalidTokens);
