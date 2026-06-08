using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.MerchantCenter;

public interface IGmcSafeBrowsingChecker
{
    Task CheckSiteAsync(ProductComplianceRun run, CancellationToken cancellationToken = default);
}

public class GmcSafeBrowsingChecker : IGmcSafeBrowsingChecker, IScopedService
{
    private readonly IRepository<ProductComplianceIssue> _issueRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<GmcSafeBrowsingChecker> _logger;

    public GmcSafeBrowsingChecker(
        IRepository<ProductComplianceIssue> issueRepo,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<GmcSafeBrowsingChecker> logger)
    {
        _issueRepo = issueRepo;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task CheckSiteAsync(ProductComplianceRun run, CancellationToken cancellationToken = default)
    {
        var apiKey = _config["Google:SafeBrowsingApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        var client = _httpClientFactory.CreateClient();
        var payload = new
        {
            client = new { clientId = "search-console-app", clientVersion = "1.0" },
            threatInfo = new
            {
                threatTypes = new[] { "MALWARE", "SOCIAL_ENGINEERING", "UNWANTED_SOFTWARE" },
                platformTypes = new[] { "ANY_PLATFORM" },
                threatEntryTypes = new[] { "URL" },
                threatEntries = new[] { new { url = run.NormalizedUrl } },
            },
        };

        try
        {
            var response = await client.PostAsJsonAsync(
                $"https://safebrowsing.googleapis.com/v4/threatMatches:find?key={apiKey}",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("matches", out var matches) || matches.GetArrayLength() == 0)
                return;

            var threat = matches[0].GetProperty("threatType").GetString() ?? "UNKNOWN";
            await _issueRepo.InsertAsync(new ProductComplianceIssue
            {
                RunId = run.Id,
                RuleId = "GMC-SAFE-001",
                Field = "site",
                Severity = ProductComplianceIssueSeverity.Critical,
                Source = ProductComplianceIssueSource.SafeBrowsing,
                Message = $"Google Safe Browsing bu siteyi işaretledi: {threat}",
                FixHint = "Kötü amaçlı yazılım, kimlik avı veya istenmeyen yazılımı araştırın; Merchant Center onayı etkilenebilir.",
                DocUrl = "https://developers.google.com/search/docs/monitor-debug/security",
                CreatedAt = DateTime.UtcNow,
            }, publishEvent: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Safe Browsing check failed for {Url}", run.NormalizedUrl);
        }
    }
}
