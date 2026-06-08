using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IExternalBacklinkService
{
    Task FetchExternalBacklinksAsync(AuditRun run, CancellationToken cancellationToken = default);
}

public record ExternalBacklinkData(
    int ReferringDomainCount,
    int BacklinkCount,
    IList<string> TopDomains,
    string Source);

/// <summary>
/// Ahrefs veya Moz API ile harici backlink profili (opsiyonel API key).
/// </summary>
public partial class ExternalBacklinkService : IExternalBacklinkService, IScopedService
{
    private readonly IRepository<BacklinkSummary> _summaryRepository;
    private readonly IAuditIssueWriter _issueWriter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IIntegrationSettingsService _integrationSettings;
    private readonly ILogger<ExternalBacklinkService> _logger;

    public ExternalBacklinkService(
        IRepository<BacklinkSummary> summaryRepository,
        IAuditIssueWriter issueWriter,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IIntegrationSettingsService integrationSettings,
        ILogger<ExternalBacklinkService> logger)
    {
        _summaryRepository = summaryRepository;
        _issueWriter = issueWriter;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _integrationSettings = integrationSettings;
        _logger = logger;
    }

    public async Task FetchExternalBacklinksAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        var summary = await _summaryRepository.Table
            .FirstOrDefaultAsync(s => s.AuditRunId == run.Id, cancellationToken);
        if (summary == null) return;

        var domain = new Uri(run.NormalizedUrl).Host;
        ExternalBacklinkData? data = null;
        if (_integrationSettings.IsEnabled("backlinks-ahrefs"))
            data = await TryAhrefsAsync(domain, cancellationToken);
        if (data == null && _integrationSettings.IsEnabled("backlinks-moz"))
            data = await TryMozAsync(domain, cancellationToken);

        if (data == null) return;

        summary.ExternalReferringDomainCount = data.ReferringDomainCount;
        summary.ExternalBacklinkCount = data.BacklinkCount;
        summary.ExternalTopDomainsJson = JsonSerializer.Serialize(data.TopDomains);
        summary.ExternalSource = data.Source;
        await _summaryRepository.UpdateAsync(summary, publishEvent: false);

        if (data.ReferringDomainCount < 5)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = run.NormalizedUrl,
                RuleId = "LINK-EXT-002",
                Category = "backlinks",
                Severity = AuditIssueSeverity.Info,
                Source = AuditIssueSource.System,
                Message = $"Harici referring domain sayısı düşük ({data.ReferringDomainCount}, kaynak: {data.Source}).",
                FixHint = "Kaliteli içerik ve dijital PR ile backlink profili oluşturun.",
                DocUrl = "https://developers.google.com/search/docs/fundamentals/creating-helpful-content",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }
    }

    private async Task<ExternalBacklinkData?> TryAhrefsAsync(string domain, CancellationToken cancellationToken)
    {
        var token = _config["Backlinks:AhrefsApiToken"];
        if (string.IsNullOrWhiteSpace(token)) return null;

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            var target = Uri.EscapeDataString(domain);
            var url =
                $"https://api.ahrefs.com/v3/site-explorer/refdomains?target={target}&mode=subdomains&limit=10&order_by=domain_rating:desc";
            var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ahrefs API returned {Status} for {Domain}", response.StatusCode, domain);
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var domains = new List<string>();
            var count = 0;

            if (json.TryGetProperty("refdomains", out var refs) && refs.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in refs.EnumerateArray())
                {
                    count++;
                    if (item.TryGetProperty("domain", out var d))
                    {
                        var name = d.GetString();
                        if (!string.IsNullOrWhiteSpace(name) && domains.Count < 10)
                            domains.Add(name);
                    }
                }
            }

            var total = json.TryGetProperty("refdomains_total", out var totalEl)
                ? totalEl.GetInt32()
                : count;

            return new ExternalBacklinkData(total, total, domains, "ahrefs");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ahrefs backlink fetch failed for {Domain}", domain);
            return null;
        }
    }

    private async Task<ExternalBacklinkData?> TryMozAsync(string domain, CancellationToken cancellationToken)
    {
        var accessId = _config["Backlinks:MozAccessId"];
        var secretKey = _config["Backlinks:MozSecretKey"];
        if (string.IsNullOrWhiteSpace(accessId) || string.IsNullOrWhiteSpace(secretKey)) return null;

        var client = _httpClientFactory.CreateClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accessId}:{secretKey}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        try
        {
            var payload = new
            {
                targets = new[] { domain },
                metrics = new[] { "root_domains_to_root_domain", "external_pages_to_root_domain" },
            };

            var response = await client.PostAsJsonAsync(
                "https://lsapi.seomoz.com/v2/url_metrics",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Moz API returned {Status} for {Domain}", response.StatusCode, domain);
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!json.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return null;

            var row = results[0];
            var refDomains = row.TryGetProperty("root_domains_to_root_domain", out var rd) ? rd.GetInt32() : 0;
            var extPages = row.TryGetProperty("external_pages_to_root_domain", out var ep) ? ep.GetInt32() : 0;

            return new ExternalBacklinkData(refDomains, extPages, [], "moz");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Moz backlink fetch failed for {Domain}", domain);
            return null;
        }
    }
}
