using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Notifications;

/// <summary>
/// Kullanıcının push notification token'ı. Bir kullanıcının birden fazla
/// cihazı olabilir (iPhone + iPad), her biri ayrı kayıt.
///
/// Şu an Expo Push token format'ı destekleniyor (ExponentPushToken[...]).
/// FCM/APNs native token desteklemek istersen Provider alanını kullan.
/// </summary>
public partial class DeviceToken : BaseEntity, ISoftDeletable
{
    public long CustomerId { get; set; }
    public string Token { get; set; } = "";
    public string Provider { get; set; } = "expo";   // expo | fcm | apns
    public string Platform { get; set; } = "";       // ios | android | web
    public string? DeviceName { get; set; }
    public string? AppVersion { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public bool Deleted { get; set; }
}
