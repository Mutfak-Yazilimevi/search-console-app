using System.Security.Cryptography;
using System.Text;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Identity;

/// <summary>
/// Cihaz parmak izi üreticisi.
///
/// Web tarafında client-side fingerprint (ekran çözünürlüğü, timezone, fonts)
/// daha kararlıdır. Backend tarafında sadece UA + Accept-Language + platform
/// bilgileriyle yetinmek zorundayız. Mobile için bundle-level instance ID
/// kullanılması önerilir (Expo Constants.installationId).
///
/// Hash girdileri:
/// - UserAgent (ana ayraç)
/// - Accept-Language
/// - Platform (User-Agent'tan parse veya header'dan)
/// - Mobile için bundleId + installationId
///
/// Sonuç: hex SHA-256 (64 char).
/// </summary>
public interface IDeviceFingerprintService
{
    string Compute(FingerprintInput input);
}

public record FingerprintInput(
    string? UserAgent,
    string? AcceptLanguage,
    string? Platform,
    string? ClientHint);

public class DeviceFingerprintService : IDeviceFingerprintService, ISingletonService
{
    public string Compute(FingerprintInput input)
    {
        // Sıralı concatenation — aynı girdi her zaman aynı hash
        var raw = string.Join("|",
            (input.UserAgent ?? "").Trim(),
            (input.AcceptLanguage ?? "").Trim(),
            (input.Platform ?? "").Trim(),
            (input.ClientHint ?? "").Trim());

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
