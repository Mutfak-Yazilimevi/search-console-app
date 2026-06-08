using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IIndexCheckService
{
    Task CheckIndexAsync(AuditRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// Google Custom Search API ile site: indeks tahmini (anonim mod).
/// </summary>
public partial class IndexCheckService : IIndexCheckService, IScopedService
{
    private readonly IRepository<IndexStatusSnapshot> _snapshotRepo;
    private readonly IAuditIssueWriter _issueWriter;
    private readonly IRepository<ScannedPage> _pageRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<IndexCheckService> _logger;

    public IndexCheckService(
        IRepository<IndexStatusSnapshot> snapshotRepo,
        IAuditIssueWriter issueWriter,
        IRepository<ScannedPage> pageRepo,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<IndexCheckService> logger)
    {
        _snapshotRepo = snapshotRepo;
        _issueWriter = issueWriter;
        _pageRepo = pageRepo;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task CheckIndexAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        var apiKey = _config["Google:CustomSearchApiKey"];
        var cx = _config["Google:CustomSearchEngineId"];
        var crawled = await _pageRepo.Table.CountAsync(p => p.AuditRunId == run.Id, cancellationToken);
        if (crawled == 0) return;

        var domain = new Uri(run.NormalizedUrl).Host;
        var estimated = 0;
        var source = "estimate";

        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(cx))
        {
            try
            {
                var q = Uri.EscapeDataString($"site:{domain}");
                var url =
                    $"https://www.googleapis.com/customsearch/v1?key={apiKey}&cx={cx}&q={q}&num=1";
                var client = _httpClientFactory.CreateClient();
                var json = await client.GetFromJsonAsync<JsonElement>(url, cancellationToken);
                if (json.TryGetProperty("searchInformation", out var si)
                    && si.TryGetProperty("totalResults", out var total))
                {
                    estimated = int.TryParse(total.GetString(), out var n) ? n : 0;
                    source = "custom-search";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Custom Search index check failed for {Domain}", domain);
            }
        }

        if (estimated == 0)
            estimated = Math.Max(1, crawled / 2);

        var ratio = crawled > 0 ? Math.Min(1.0, (double)estimated / crawled) : 0;

        await _snapshotRepo.InsertAsync(new IndexStatusSnapshot
        {
            AuditRunId = run.Id,
            Domain = domain,
            EstimatedIndexedPages = estimated,
            CrawledPages = crawled,
            CoverageRatio = ratio,
            Source = source,
            DetailsJson = JsonSerializer.Serialize(new { note = source == "estimate" ? "API key yok — tahmini" : "Custom Search" }),
            CreatedAtUtc = DateTime.UtcNow,
        }, publishEvent: false);

        if (ratio < 0.7 && crawled >= 5)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = run.NormalizedUrl,
                RuleId = "INDEX-002",
                Category = "index-status",
                Severity = AuditIssueSeverity.Warning,
                Source = AuditIssueSource.Serp,
                Message = $"İndeks kapsama oranı düşük: ~{estimated} indeksli / {crawled} taranan sayfa.",
                FixHint = "noindex, robots.txt ve içerik kalitesini kontrol edin; Search Console URL Denetimi kullanın.",
                DocUrl = "https://developers.google.com/search/docs/crawling-indexing/robots-meta-tag",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }

        await CheckPageLevelIndexAsync(run, apiKey, cx, cancellationToken);
    }

    private async Task CheckPageLevelIndexAsync(
        AuditRun run, string? apiKey, string? cx, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(cx)) return;

        var maxPages = _config.GetValue("Audit:PageIndexCheckMaxPages", 20);
        var urls = await _pageRepo.Table
            .Where(p => p.AuditRunId == run.Id)
            .OrderBy(p => p.CrawlDepth)
            .Select(p => p.Url)
            .Take(maxPages)
            .ToListAsync(cancellationToken);

        if (urls.Count == 0) return;

        var client = _httpClientFactory.CreateClient();
        var notIndexed = 0;

        foreach (var pageUrl in urls)
        {
            try
            {
                var q = Uri.EscapeDataString($"site:{pageUrl}");
                var url = $"https://www.googleapis.com/customsearch/v1?key={apiKey}&cx={cx}&q={q}&num=1";
                var json = await client.GetFromJsonAsync<JsonElement>(url, cancellationToken);
                var total = 0;
                if (json.TryGetProperty("searchInformation", out var si)
                    && si.TryGetProperty("totalResults", out var totalEl))
                {
                    total = int.TryParse(totalEl.GetString(), out var n) ? n : 0;
                }

                if (total > 0) continue;
                notIndexed++;

                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = pageUrl,
                    RuleId = "INDEX-003",
                    Category = "index-status",
                    Severity = AuditIssueSeverity.Info,
                    Source = AuditIssueSource.Serp,
                    Message = "Sayfa Google indeksinde görünmüyor (site: sorgusu).",
                    FixHint = "URL Denetimi ile indeks durumunu kontrol edin.",
                    DocUrl = "https://developers.google.com/search/docs/monitor-debug/search-console-start",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);

                if (notIndexed >= 5) break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Page index check failed for {Url}", pageUrl);
            }
        }
    }
}
