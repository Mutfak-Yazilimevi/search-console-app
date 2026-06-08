using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IKeywordSerpCheckService
{
    Task CheckKeywordsAsync(AuditRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// Kullanıcı tanımlı anahtar kelimeler için Google Custom Search SERP pozisyon kontrolü.
/// </summary>
public partial class KeywordSerpCheckService : IKeywordSerpCheckService, IScopedService
{
    private readonly IRepository<SiteKeywordWatch> _watchRepository;
    private readonly IRepository<KeywordSerpSnapshot> _snapshotRepository;
    private readonly IRepository<AuditRun> _auditRunRepository;
    private readonly IAuditIssueWriter _issueWriter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<KeywordSerpCheckService> _logger;

    public KeywordSerpCheckService(
        IRepository<SiteKeywordWatch> watchRepository,
        IRepository<KeywordSerpSnapshot> snapshotRepository,
        IRepository<AuditRun> auditRunRepository,
        IAuditIssueWriter issueWriter,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<KeywordSerpCheckService> logger)
    {
        _watchRepository = watchRepository;
        _snapshotRepository = snapshotRepository;
        _auditRunRepository = auditRunRepository;
        _issueWriter = issueWriter;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task CheckKeywordsAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        if (run.CustomerId is not { } customerId) return;

        var apiKey = _config["Google:CustomSearchApiKey"];
        var cx = _config["Google:CustomSearchEngineId"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(cx))
        {
            _logger.LogDebug("Custom Search not configured; keyword SERP check skipped");
            return;
        }

        var host = new Uri(run.NormalizedUrl).Host.ToLowerInvariant();
        var watches = await _watchRepository.Table
            .Where(w => w.CustomerId == customerId && w.SiteHost == host && w.IsEnabled)
            .Take(15)
            .ToListAsync(cancellationToken);

        if (watches.Count == 0) return;

        var maxKeywords = _config.GetValue("Audit:KeywordSerpMaxKeywords", 10);
        var client = _httpClientFactory.CreateClient();

        foreach (var watch in watches.Take(maxKeywords))
        {
            var (position, matchedUrl) = await QuerySerpPositionAsync(
                client, apiKey, cx, watch.Keyword, host, cancellationToken);

            await _snapshotRepository.InsertAsync(new KeywordSerpSnapshot
            {
                AuditRunId = run.Id,
                Keyword = watch.Keyword,
                Position = position,
                MatchedUrl = matchedUrl,
                CreatedAtUtc = DateTime.UtcNow,
            }, publishEvent: false);

            if (position == 0)
            {
                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = run.NormalizedUrl,
                    RuleId = "RANK-005",
                    Category = "ranking",
                    Severity = AuditIssueSeverity.Warning,
                    Source = AuditIssueSource.Serp,
                    Message = $"Anahtar kelime ilk 10 SERP sonucunda görünmüyor: \"{watch.Keyword}\".",
                    FixHint = "İçerik hedeflemesi, iç linkler ve teknik indeks engellerini gözden geçirin.",
                    DocUrl = "https://developers.google.com/search/docs/fundamentals/seo-starter-guide",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);
                continue;
            }

            var previous = await FindPreviousPositionAsync(run, host, watch.Keyword, cancellationToken);
            if (previous.HasValue && previous.Value > 0 && position - previous.Value >= 3)
            {
                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = matchedUrl ?? run.NormalizedUrl,
                    RuleId = "RANK-006",
                    Category = "ranking",
                    Severity = AuditIssueSeverity.Info,
                    Source = AuditIssueSource.Serp,
                    Message = $"SERP pozisyon düşüşü: \"{watch.Keyword}\" {previous} → {position}.",
                    FixHint = "Rakip içerik ve snippet kalitesini karşılaştırın.",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);
            }
        }
    }

    private async Task<int?> FindPreviousPositionAsync(
        AuditRun run, string host, string keyword, CancellationToken cancellationToken)
    {
        var previousRun = await _auditRunRepository.Table
            .Where(r => r.Id != run.Id
                && r.CustomerId == run.CustomerId
                && r.Status == AuditRunStatus.Completed
                && r.NormalizedUrl.Contains(host))
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousRun == null) return null;

        var snapshot = await _snapshotRepository.Table
            .FirstOrDefaultAsync(s => s.AuditRunId == previousRun.Id && s.Keyword == keyword, cancellationToken);

        return snapshot?.Position;
    }

    private async Task<(int Position, string? MatchedUrl)> QuerySerpPositionAsync(
        HttpClient client, string apiKey, string cx, string keyword, string host,
        CancellationToken cancellationToken)
    {
        try
        {
            var q = Uri.EscapeDataString(keyword);
            var url = $"https://www.googleapis.com/customsearch/v1?key={apiKey}&cx={cx}&q={q}&num=10";
            var json = await client.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            if (!json.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return (0, null);

            var index = 0;
            foreach (var item in items.EnumerateArray())
            {
                index++;
                if (!item.TryGetProperty("link", out var linkEl)) continue;
                var link = linkEl.GetString() ?? "";
                if (!link.Contains(host, StringComparison.OrdinalIgnoreCase)) continue;
                return (index, link);
            }

            return (0, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SERP check failed for keyword {Keyword}", keyword);
            return (0, null);
        }
    }
}
