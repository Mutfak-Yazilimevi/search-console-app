using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Localization;

/// <summary>
/// Backend message localization. JSON dosyalarından okur.
///
/// Locale çözümleme öncelik sırası:
/// 1. Accept-Language header (kullanıcı tercihi)
/// 2. Customer.Language (DB'de saklı tercih — entity'ye eklenirse)
/// 3. Config'teki default ("App:DefaultLanguage")
///
/// JSON formatı (Resources/messages.{lang}.json):
/// {
///   "auth.invalid_credentials": "Email veya şifre hatalı.",
///   "auth.account_disabled": "Hesap aktif değil."
/// }
///
/// Eksik key fallback'i: önce default locale, sonra key kendisi.
/// Production'da Resource manager kullanan ASP.NET Core IStringLocalizer da
/// tercih edilebilir — bu impl daha basit ve embedded JSON kullanımı kolay.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Mesajı çevir. Parametreler {0}, {1} ile interpolasyon.</summary>
    string Get(string key, params object[] args);

    /// <summary>Aktif locale'i değiştir (genelde middleware set eder).</summary>
    string CurrentLanguage { get; }
}

/// <summary>Scoped — her request kendi locale'iyle.</summary>
public class JsonLocalizationService : ILocalizationService, IScopedService
{
    private static readonly Dictionary<string, Dictionary<string, string>> _cache = new();
    private static readonly object _cacheLock = new();
    private static bool _loaded;

    private readonly string _defaultLanguage;
    private readonly ILogger<JsonLocalizationService> _logger;

    public string CurrentLanguage { get; set; }

    public JsonLocalizationService(
        Microsoft.Extensions.Configuration.IConfiguration config,
        ILogger<JsonLocalizationService> logger)
    {
        _defaultLanguage = config["App:DefaultLanguage"] ?? "en";
        CurrentLanguage = _defaultLanguage;
        _logger = logger;

        EnsureLoaded();
    }

    public string Get(string key, params object[] args)
    {
        var message = LookupInLanguage(CurrentLanguage, key)
                   ?? LookupInLanguage(_defaultLanguage, key)
                   ?? key;  // son çare: key'in kendisi

        return args.Length > 0
            ? string.Format(CultureInfo.InvariantCulture, message, args)
            : message;
    }

    private static string? LookupInLanguage(string lang, string key)
    {
        return _cache.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var msg) ? msg : null;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_cacheLock)
        {
            if (_loaded) return;

            var resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
            if (!Directory.Exists(resourcesDir))
            {
                _logger.LogWarning("Localization resources klasörü yok: {Dir}", resourcesDir);
                _loaded = true;
                return;
            }

            foreach (var file in Directory.GetFiles(resourcesDir, "messages.*.json"))
            {
                try
                {
                    // messages.tr.json → "tr"
                    var name = Path.GetFileNameWithoutExtension(file);
                    var lang = name.Split('.').Last();

                    var json = File.ReadAllText(file);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                    _cache[lang] = dict;

                    _logger.LogInformation("Localization yüklendi: {Lang} ({Count} mesaj)", lang, dict.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Localization dosyası yüklenemedi: {File}", file);
                }
            }

            _loaded = true;
        }
    }
}
