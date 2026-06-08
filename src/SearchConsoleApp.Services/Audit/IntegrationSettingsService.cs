using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Audit;

public sealed class IntegrationFieldDefinition
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public bool IsSecret { get; init; } = true;
}

public sealed class IntegrationDefinition
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public bool CanToggle { get; init; } = true;
    public bool Required { get; init; }
    public IList<IntegrationFieldDefinition> Fields { get; init; } = [];
}

public sealed class IntegrationFieldStatus
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public bool IsSecret { get; init; }
    public bool HasValue { get; init; }
    public string? MaskedValue { get; init; }
}

public sealed class IntegrationSettingsItem
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public bool CanToggle { get; init; } = true;
    public string Status { get; init; } = "";
    public string? Detail { get; init; }
    public string? ConfigKey { get; init; }
    public IList<IntegrationFieldStatus> Fields { get; init; } = [];
}

public sealed class IntegrationSettingsResponse
{
    public IList<IntegrationSettingsItem> Integrations { get; init; } = [];
}

public sealed class UpdateIntegrationRequest
{
    public bool? Enabled { get; init; }
    public IDictionary<string, string>? Values { get; init; }
}

public interface IIntegrationSettingsService
{
    IReadOnlyList<IntegrationDefinition> Definitions { get; }
    IntegrationSettingsResponse GetAll(bool searchConsoleConnected = false);
    bool IsEnabled(string integrationId);
    Task<IntegrationSettingsItem> UpdateAsync(string integrationId, UpdateIntegrationRequest request, CancellationToken cancellationToken = default);
}

public partial class IntegrationSettingsService : IIntegrationSettingsService, IScopedService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly IReadOnlyList<IntegrationDefinition> DefaultDefinitions =
    [
        new()
        {
            Id = "crawl-worker",
            Label = "Crawl worker",
            CanToggle = false,
            Required = true,
            Fields = [new IntegrationFieldDefinition { Key = "Audit:CrawlWorkerUrl", Label = "Worker URL", IsSecret = false }],
        },
        new()
        {
            Id = "pagespeed",
            Label = "PageSpeed Insights",
            Fields = [new IntegrationFieldDefinition { Key = "Google:PageSpeedApiKey", Label = "API anahtarı" }],
        },
        new()
        {
            Id = "safe-browsing",
            Label = "Safe Browsing",
            Fields = [new IntegrationFieldDefinition { Key = "Google:SafeBrowsingApiKey", Label = "API anahtarı" }],
        },
        new()
        {
            Id = "custom-search",
            Label = "Custom Search (index/SERP)",
            Fields =
            [
                new IntegrationFieldDefinition { Key = "Google:CustomSearchApiKey", Label = "API anahtarı" },
                new IntegrationFieldDefinition { Key = "Google:CustomSearchEngineId", Label = "Search Engine ID", IsSecret = false },
            ],
        },
        new()
        {
            Id = "gemini",
            Label = "Gemini (SSS AI)",
            Fields = [new IntegrationFieldDefinition { Key = "Google:GeminiApiKey", Label = "API anahtarı" }],
        },
        new()
        {
            Id = "llm-eeat",
            Label = "LLM E-E-A-T",
            Fields = [new IntegrationFieldDefinition { Key = "Llm:ApiKey", Label = "API anahtarı" }],
        },
        new()
        {
            Id = "oauth-google",
            Label = "Google OAuth",
            Fields =
            [
                new IntegrationFieldDefinition { Key = "OAuth:google:ClientId", Label = "Client ID", IsSecret = false },
                new IntegrationFieldDefinition { Key = "OAuth:google:ClientSecret", Label = "Client secret" },
            ],
        },
        new()
        {
            Id = "backlinks-ahrefs",
            Label = "Ahrefs backlink",
            Fields = [new IntegrationFieldDefinition { Key = "Backlinks:AhrefsApiToken", Label = "API token" }],
        },
        new()
        {
            Id = "backlinks-moz",
            Label = "Moz backlink",
            Fields =
            [
                new IntegrationFieldDefinition { Key = "Backlinks:MozAccessId", Label = "Access ID", IsSecret = false },
                new IntegrationFieldDefinition { Key = "Backlinks:MozSecretKey", Label = "Secret key" },
            ],
        },
        new()
        {
            Id = "search-console",
            Label = "Search Console OAuth",
            CanToggle = true,
            Fields =
            [
                new IntegrationFieldDefinition { Key = "GoogleSearchConsole:ClientId", Label = "Client ID", IsSecret = false },
                new IntegrationFieldDefinition { Key = "GoogleSearchConsole:ClientSecret", Label = "Client secret" },
            ],
        },
        new()
        {
            Id = "merchant-center-oauth",
            Label = "Merchant Center OAuth",
            CanToggle = false,
            Fields =
            [
                new IntegrationFieldDefinition { Key = "Google:MerchantCenter:ClientId", Label = "Client ID", IsSecret = false },
                new IntegrationFieldDefinition { Key = "Google:MerchantCenter:ClientSecret", Label = "Client secret" },
                new IntegrationFieldDefinition { Key = "Google:MerchantCenter:RedirectUri", Label = "Redirect URI", IsSecret = false },
            ],
        },
    ];

    private readonly IConfiguration _config;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<IntegrationSettingsService> _logger;

    public IntegrationSettingsService(
        IConfiguration config,
        IHostEnvironment environment,
        ILogger<IntegrationSettingsService> logger)
    {
        _config = config;
        _environment = environment;
        _logger = logger;
    }

    public IReadOnlyList<IntegrationDefinition> Definitions => DefaultDefinitions;

    public IntegrationSettingsResponse GetAll(bool searchConsoleConnected = false)
    {
        var items = Definitions
            .Select(def => BuildItem(def, searchConsoleConnected))
            .ToList();
        return new IntegrationSettingsResponse { Integrations = items };
    }

    public bool IsEnabled(string integrationId)
    {
        var def = Definitions.FirstOrDefault(d => d.Id == integrationId);
        if (def == null) return true;
        if (!def.CanToggle || def.Required) return true;
        return _config.GetValue($"Audit:Integrations:{integrationId}:Enabled", true);
    }

    public async Task<IntegrationSettingsItem> UpdateAsync(
        string integrationId,
        UpdateIntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var def = Definitions.FirstOrDefault(d => d.Id == integrationId)
            ?? throw new InvalidOperationException($"Unknown integration: {integrationId}");

        var root = await LoadOverridesRootAsync(cancellationToken);

        if (request.Enabled.HasValue && def.CanToggle)
            SetNestedValue(root, $"Audit:Integrations:{integrationId}:Enabled", request.Enabled.Value);

        if (request.Values != null)
        {
            foreach (var field in def.Fields)
            {
                if (!request.Values.TryGetValue(field.Key, out var value))
                    continue;
                if (string.IsNullOrWhiteSpace(value))
                    RemoveNestedValue(root, field.Key);
                else
                    SetNestedValue(root, field.Key, value.Trim());
            }
        }

        await SaveOverridesRootAsync(root, cancellationToken);
        if (_config is IConfigurationRoot configurationRoot)
            configurationRoot.Reload();
        _logger.LogInformation("Integration settings updated: {IntegrationId}", integrationId);
        return BuildItem(def, searchConsoleConnected: false);
    }

    private IntegrationSettingsItem BuildItem(IntegrationDefinition def, bool searchConsoleConnected)
    {
        if (def.Id == "search-console")
        {
            var credsConfigured = def.Fields.All(f => !string.IsNullOrWhiteSpace(_config[f.Key]));
            var detail = searchConsoleConnected
                ? "Hesap bağlı"
                : !IsEnabled(def.Id)
                    ? "Devre dışı"
                    : credsConfigured
                        ? "Kimlik bilgileri kayıtlı — Google ile giriş yapıp Search Console bağlayın"
                        : "Client ID/secret girin, ardından Google ile giriş yapıp Search Console bağlayın";

            return new IntegrationSettingsItem
            {
                Id = def.Id,
                Label = def.Label,
                Enabled = IsEnabled(def.Id),
                CanToggle = def.CanToggle,
                Status = searchConsoleConnected ? "connected" : !IsEnabled(def.Id) ? "disabled" : credsConfigured ? "configured" : "not_connected",
                Detail = detail,
                ConfigKey = "GoogleSearchConsole:ClientId",
                Fields = BuildFieldStatuses(def),
            };
        }

        if (def.Id == "merchant-center-oauth")
        {
            var clientId = _config["Google:MerchantCenter:ClientId"] ?? _config["OAuth:google:ClientId"];
            var secret = _config["Google:MerchantCenter:ClientSecret"] ?? _config["OAuth:google:ClientSecret"];
            var gmcConfigured = !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(secret);

            return new IntegrationSettingsItem
            {
                Id = def.Id,
                Label = def.Label,
                Enabled = true,
                CanToggle = def.CanToggle,
                Status = gmcConfigured ? "configured" : "missing",
                ConfigKey = "Google:MerchantCenter:ClientId",
                Detail = gmcConfigured
                    ? "OAuth kimlik bilgileri kayıtlı — Merchant Center'a Bağlan ile hesabınızı bağlayın"
                    : "Client ID/secret girin; ardından Merchant Center'a Bağlan",
                Fields = BuildFieldStatuses(def),
            };
        }

        var enabled = IsEnabled(def.Id);
        var configured = def.Fields.All(f => !string.IsNullOrWhiteSpace(_config[f.Key]));
        var status = !enabled ? "disabled" : configured ? "configured" : "missing";
        var primaryKey = def.Fields.FirstOrDefault()?.Key;

        return new IntegrationSettingsItem
        {
            Id = def.Id,
            Label = def.Label,
            Enabled = enabled,
            CanToggle = def.CanToggle,
            Status = status,
            ConfigKey = primaryKey,
            Detail = StatusDetail(status, primaryKey, enabled),
            Fields = BuildFieldStatuses(def),
        };
    }

    private IList<IntegrationFieldStatus> BuildFieldStatuses(IntegrationDefinition def)
        => def.Fields.Select(f =>
        {
            var value = _config[f.Key];
            var hasValue = !string.IsNullOrWhiteSpace(value);
            return new IntegrationFieldStatus
            {
                Key = f.Key,
                Label = f.Label,
                IsSecret = f.IsSecret,
                HasValue = hasValue,
                MaskedValue = hasValue ? MaskValue(value!, f.IsSecret) : null,
            };
        }).ToList();

    private static string? StatusDetail(string status, string? configKey, bool enabled)
    {
        if (!enabled) return "Devre dışı";
        return status switch
        {
            "configured" => "Yapılandırıldı — tıklayarak güncelleyebilirsiniz",
            "missing" => configKey != null ? "Eksik — tıklayıp değer girin" : "Eksik",
            _ => null,
        };
    }

    private static string MaskValue(string value, bool isSecret)
    {
        if (!isSecret) return value;
        if (value.Length <= 4) return "••••";
        return $"{new string('•', Math.Min(8, value.Length - 4))}{value[^4..]}";
    }

    private string OverridesPath =>
        Path.Combine(_environment.ContentRootPath, "App_Data", "integration-overrides.json");

    private async Task<JsonObject> LoadOverridesRootAsync(CancellationToken cancellationToken)
    {
        var path = OverridesPath;
        if (!File.Exists(path))
            return new JsonObject();

        await using var stream = File.OpenRead(path);
        var node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        return node as JsonObject ?? new JsonObject();
    }

    private async Task SaveOverridesRootAsync(JsonObject root, CancellationToken cancellationToken)
    {
        var path = OverridesPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, root, JsonOptions, cancellationToken);
    }

    private static void SetNestedValue(JsonObject root, string configKey, object value)
    {
        var parts = configKey.Split(':');
        JsonObject current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            if (current[part] is not JsonObject child)
            {
                child = new JsonObject();
                current[part] = child;
            }
            current = child;
        }

        current[parts[^1]] = value switch
        {
            bool b => JsonValue.Create(b),
            string s => s,
            _ => JsonValue.Create(value),
        };
    }

    private static void RemoveNestedValue(JsonObject root, string configKey)
    {
        var parts = configKey.Split(':');
        JsonObject current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (current[parts[i]] is not JsonObject child)
                return;
            current = child;
        }
        current.Remove(parts[^1]);
    }
}
