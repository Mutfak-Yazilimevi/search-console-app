using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Audit.SearchConsole;

namespace SearchConsoleApp.Services.Audit;

public interface ISearchConsoleCoverageService
{
    Task AnalyzeAsync(
        AuditRun run,
        string accessToken,
        string propertyUrl,
        SearchConsoleAuditData scData,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Search Console sitemap, URL denetimi kapsamı ve manuel işlem sinyalleri.
/// </summary>
public partial class SearchConsoleCoverageService : ISearchConsoleCoverageService, IScopedService
{
    private readonly IAuditIssueWriter _issueWriter;
    private readonly IRepository<SearchConsoleSnapshot> _snapshotRepository;
    private readonly ISearchConsoleApiClient _scApi;
    private readonly ILogger<SearchConsoleCoverageService> _logger;

    public SearchConsoleCoverageService(
        IAuditIssueWriter issueWriter,
        IRepository<SearchConsoleSnapshot> snapshotRepository,
        ISearchConsoleApiClient scApi,
        ILogger<SearchConsoleCoverageService> logger)
    {
        _issueWriter = issueWriter;
        _snapshotRepository = snapshotRepository;
        _scApi = scApi;
        _logger = logger;
    }

    public async Task AnalyzeAsync(
        AuditRun run,
        string accessToken,
        string propertyUrl,
        SearchConsoleAuditData scData,
        CancellationToken cancellationToken = default)
    {
        var sitemaps = await _scApi.ListSitemapsAsync(accessToken, propertyUrl, cancellationToken);

        var passed = scData.UrlInspections.Count(i =>
            i.Verdict is "PASS" or "NEUTRAL");
        var failed = scData.UrlInspections.Count - passed;
        var sitemapErrors = sitemaps.Sum(s => s.Errors);
        var sitemapWarnings = sitemaps.Sum(s => s.Warnings);

        var snapshot = await _snapshotRepository.Table
            .FirstOrDefaultAsync(s => s.AuditRunId == run.Id, cancellationToken);

        if (snapshot != null)
        {
            snapshot.IndexedPages = passed;
            snapshot.ExcludedPages = failed;
            snapshot.SitemapsJson = JsonSerializer.Serialize(new
            {
                sitemaps = sitemaps.Select(s => new
                {
                    s.Path,
                    s.Errors,
                    s.Warnings,
                    s.IsPending,
                    s.LastDownloaded,
                }),
                inspectionSummary = new
                {
                    inspected = scData.UrlInspections.Count,
                    passed,
                    failed,
                },
            });
            await _snapshotRepository.UpdateAsync(snapshot, publishEvent: false);
        }

        if (scData.UrlInspections.Count >= 3)
        {
            var passRate = (double)passed / scData.UrlInspections.Count;
            if (passRate < 0.6)
            {
                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = run.NormalizedUrl,
                    RuleId = "SC-002",
                    Category = "search-console",
                    Severity = AuditIssueSeverity.Warning,
                    Source = AuditIssueSource.SearchConsole,
                    Message = $"Search Console URL Denetimi: indeks kapsama oranı düşük ({passed}/{scData.UrlInspections.Count} geçti).",
                    FixHint = "Başarısız URL'lerde noindex, robots.txt, canonical ve içerik kalitesini kontrol edin.",
                    DocUrl = "https://developers.google.com/search/docs/monitor-debug/search-console-start",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);
            }
        }

        foreach (var inspection in scData.UrlInspections)
        {
            if (!ContainsManualActionSignal(inspection)) continue;

            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = inspection.Url,
                RuleId = "SC-003",
                Category = "search-console",
                Severity = AuditIssueSeverity.Critical,
                Source = AuditIssueSource.SearchConsole,
                Message = $"Search Console manuel işlem veya ciddi indeks engeli: {inspection.CoverageState}",
                Evidence = IssueDetailEvidenceBuilder.PageElement(
                    $"Search Console manuel işlem: {inspection.CoverageState}",
                    inspection.Url,
                    inspection.IndexingState ?? inspection.CoverageState ?? "—",
                    "Search Console → Manuel işlemler ve Güvenlik sorunları bölümlerini kontrol edin"),
                FixHint = "Search Console → Manuel işlemler ve Güvenlik sorunları bölümlerini kontrol edin.",
                DocUrl = "https://developers.google.com/search/docs/monitor-debug/security",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
            break;
        }

        if (sitemapErrors > 0)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = run.NormalizedUrl,
                RuleId = "SC-004",
                Category = "search-console",
                Severity = AuditIssueSeverity.Warning,
                Source = AuditIssueSource.SearchConsole,
                Message = $"Search Console site haritasında {sitemapErrors} hata.",
                Evidence = IssueDetailEvidenceBuilder.Build(
                    $"Search Console site haritasında {sitemapErrors} hata",
                    sitemaps.Where(s => s.Errors > 0).Take(5).Select(s => new IssueDetailItemDto
                    {
                        Label = "Sitemap",
                        Value = s.Path,
                        Detail = $"{s.Errors} hata",
                    })),
                FixHint = "Site Haritaları raporundaki hatalı URL'leri düzeltin.",
                DocUrl = "https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }
        else if (sitemapWarnings > 0 && sitemaps.Count > 0)
        {
            _logger.LogInformation(
                "SC sitemap warnings for audit {EntityId}: {Warnings}",
                run.EntityId, sitemapWarnings);
        }
    }

    private static bool ContainsManualActionSignal(UrlInspectionResult inspection)
    {
        var text = $"{inspection.CoverageState} {inspection.IndexingState}".ToLowerInvariant();
        return text.Contains("manual action", StringComparison.Ordinal)
            || text.Contains("manual işlem", StringComparison.Ordinal)
            || text.Contains("security issue", StringComparison.Ordinal)
            || text.Contains("güvenlik", StringComparison.Ordinal);
    }
}
