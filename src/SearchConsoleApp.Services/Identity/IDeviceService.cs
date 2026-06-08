using SearchConsoleApp.Core.Domain.Identity;

namespace SearchConsoleApp.Services.Identity;

public interface IDeviceService
{
    /// <summary>Fingerprint ile cihaz bul veya yeni oluştur.</summary>
    Task<Device> GetOrCreateAsync(long customerId, FingerprintInput input, string deviceType);

    Task<Device?> GetByIdAsync(long deviceId);
    Task<Device?> GetByEntityIdAsync(Guid entityId);

    /// <summary>Kullanıcının tüm cihazları.</summary>
    Task<IList<Device>> GetByCustomerAsync(long customerId);

    /// <summary>Kullanıcı cihaza güvenir/cüvenmez işaretler.</summary>
    Task SetTrustedAsync(long deviceId, bool trusted);

    /// <summary>Kullanıcı cihaza isim verir.</summary>
    Task RenameAsync(long deviceId, string newName);

    /// <summary>Cihazı sil — tüm aktif session'ları revoke edilmeli (SessionService).</summary>
    Task DeleteAsync(long deviceId);
}
