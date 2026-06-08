using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core.FeatureFlags;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Core.RequestScope;

namespace SearchConsoleApp.Services.FeatureFlags;

/// <summary>
/// Config-driven feature flag implementation.
///
/// appsettings.json format:
/// "FeatureFlags": {
///   "new-checkout-flow": false,
///   "beta-admin-ui": {
///     "default": false,
///     "rolloutPercent": 10,         // %10 kullanıcıya açık
///     "enabledForRoles": ["admin"], // admin'lere her zaman açık
///     "enabledForCustomerIds": [42, 88]
///   }
/// }
///
/// Targeting algoritması (priority):
/// 1. enabledForCustomerIds match → açık
/// 2. enabledForRoles match → açık
/// 3. rolloutPercent &gt; 0 → CustomerId hash'i % 100 &lt; rolloutPercent → açık
/// 4. default değer
///
/// Production'da: LaunchDarkly/Unleash/ConfigCat — bu impl'in yerine geçer,
/// IFeatureFlags interface aynı kalır.
/// </summary>
public class InProcessFeatureFlags : IFeatureFlags, IScopedService
{
    private readonly IConfiguration _config;
    private readonly IRequestScope _scope;

    public InProcessFeatureFlags(IConfiguration config, IRequestScope scope)
    {
        _config = config;
        _scope = scope;
    }

    public Task<bool> IsEnabledAsync(string flagKey, bool defaultValue = false, EvaluationContext? context = null)
    {
        var section = _config.GetSection($"FeatureFlags:{flagKey}");
        if (!section.Exists()) return Task.FromResult(defaultValue);

        // Basit boolean değer: "FeatureFlags:my-flag": true
        if (section.Value != null)
        {
            return Task.FromResult(bool.TryParse(section.Value, out var v) ? v : defaultValue);
        }

        // Object form: targeting
        var defaultEnabled = section.GetValue("default", defaultValue);
        var customerId = context?.TargetingKey ?? _scope.CustomerId?.ToString();

        // Customer ID match
        var enabledIds = section.GetSection("enabledForCustomerIds").Get<long[]>() ?? Array.Empty<long>();
        if (customerId != null && long.TryParse(customerId, out var cid) && enabledIds.Contains(cid))
            return Task.FromResult(true);

        // Role match (context'ten veya request claim'inden)
        var enabledRoles = section.GetSection("enabledForRoles").Get<string[]>() ?? Array.Empty<string>();
        if (enabledRoles.Length > 0 && context?.Attributes.TryGetValue("roles", out var rolesObj) == true
            && rolesObj is IEnumerable<string> roles)
        {
            if (enabledRoles.Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase)))
                return Task.FromResult(true);
        }

        // Rollout percent — sticky bucketing
        var rolloutPercent = section.GetValue("rolloutPercent", 0);
        if (rolloutPercent > 0 && customerId != null)
        {
            var bucket = StableBucket(flagKey, customerId);
            if (bucket < rolloutPercent) return Task.FromResult(true);
        }

        return Task.FromResult(defaultEnabled);
    }

    public Task<string> GetStringAsync(string flagKey, string defaultValue = "", EvaluationContext? context = null)
    {
        var value = _config[$"FeatureFlags:{flagKey}"];
        return Task.FromResult(value ?? defaultValue);
    }

    public Task<double> GetNumberAsync(string flagKey, double defaultValue = 0, EvaluationContext? context = null)
    {
        var section = _config.GetSection($"FeatureFlags:{flagKey}");
        if (!section.Exists() || section.Value == null) return Task.FromResult(defaultValue);
        return Task.FromResult(double.TryParse(section.Value, out var v) ? v : defaultValue);
    }

    /// <summary>
    /// Stable hash bucketing: aynı kullanıcı + aynı flag = aynı bucket (0-99).
    /// Rollout %10 → bucket 0-9 alanlar açık, diğerleri kapalı.
    /// MD5 deterministic, hızlı, kriptografik güvenlik aranmıyor.
    /// </summary>
    private static int StableBucket(string flagKey, string targetingKey)
    {
        var input = $"{flagKey}:{targetingKey}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        // İlk 4 byte → uint → % 100
        var n = BitConverter.ToUInt32(hash, 0);
        return (int)(n % 100);
    }
}
