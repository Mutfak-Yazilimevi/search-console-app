namespace SearchConsoleApp.Core.FeatureFlags;

/// <summary>
/// Feature flag erişim arayüzü.
///
/// OpenFeature SDK üzerinde ince wrapper. Provider-agnostic — ileride
/// LaunchDarkly, Unleash, ConfigCat, Split.io gibi yönetilen servislere
/// geçişte sadece DI registration değişir.
///
/// Default impl: `InProcessFeatureFlags` — appsettings'ten okur.
/// Production: gerçek provider (LaunchDarkly vb.) tercih edilir.
///
/// Context targeting:
/// Flag değeri kullanıcıya göre değişebilir (örn. beta kullanıcılarına aç).
/// `EvaluationContext` ile customerId, audience, role gibi attribute'lar
/// provider'a iletilir.
/// </summary>
public interface IFeatureFlags
{
    /// <summary>Boolean flag. False default — flag yoksa veya kapalıysa.</summary>
    Task<bool> IsEnabledAsync(string flagKey, bool defaultValue = false, EvaluationContext? context = null);

    /// <summary>String flag — variant'lar için ("control", "treatment-a", "treatment-b").</summary>
    Task<string> GetStringAsync(string flagKey, string defaultValue = "", EvaluationContext? context = null);

    /// <summary>Number flag — limit/threshold için.</summary>
    Task<double> GetNumberAsync(string flagKey, double defaultValue = 0, EvaluationContext? context = null);
}

/// <summary>
/// Flag evaluation context. Provider bunu kullanıcıya/audience'a/role'e göre
/// değer döndürmek için kullanır.
///
/// `TargetingKey` — provider'a göre değişir (LaunchDarkly: user.key, Unleash: userId).
/// `Attributes` — ek metadata.
/// </summary>
public class EvaluationContext
{
    public string? TargetingKey { get; set; }
    public Dictionary<string, object> Attributes { get; set; } = new();
}

/// <summary>Sistem genelinde standardize flag isimleri.</summary>
public static class FeatureFlagKeys
{
    public const string NewCheckoutFlow = "new-checkout-flow";
    public const string SignalRPushNotifications = "signalr-push-notifications";
    public const string AggressiveRateLimit = "aggressive-rate-limit";
    public const string MaintenanceMode = "maintenance-mode";
    public const string BetaAdminUi = "beta-admin-ui";
}
