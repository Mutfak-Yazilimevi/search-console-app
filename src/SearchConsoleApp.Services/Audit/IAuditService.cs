using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Services.Audit;

public interface IAuditService
{
    Task<AuditRun> StartAuditAsync(string url, CancellationToken cancellationToken = default);
    Task<AuditRun> StartConnectedAuditAsync(
        string url,
        long customerId,
        string? searchConsolePropertyUrl,
        CancellationToken cancellationToken = default);
    Task<AuditRun> StartScheduledAuditAsync(
        string url,
        long customerId,
        long scheduledAuditId,
        string? searchConsolePropertyUrl,
        CancellationToken cancellationToken = default);
    Task<AuditRunDetail?> GetAuditAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<SearchConsolePerformanceDetail?> GetPerformanceAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<IList<ContentQualityScore>> GetContentQualityAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<IList<PageSpeedResult>> GetPageSpeedAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<IndexStatusSnapshot?> GetIndexStatusAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<BacklinkSummary?> GetBacklinksAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<IList<TrackedKeyword>> GetTrackedKeywordsAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<IList<KeywordSerpSnapshot>> GetKeywordSerpAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<SearchConsoleCoverageDetail?> GetSearchConsoleCoverageAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<AuditExportDto?> ExportAuditAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<string?> ExportHtmlReportAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<string?> ExportCriticalHtmlReportAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task EnqueueCrawlJobAsync(AuditRun run, CancellationToken cancellationToken = default);
    Task ProcessCrawlPageAsync(Guid auditRunEntityId, CrawlPagePayload payload, CancellationToken cancellationToken = default);
    Task CompleteCrawlAsync(Guid auditRunEntityId, CrawlCompletePayload payload, CancellationToken cancellationToken = default);
    Task FailCrawlAsync(Guid auditRunEntityId, string errorMessage, CancellationToken cancellationToken = default);
    Task<AuditRun?> CancelAuditAsync(Guid auditRunEntityId, CancellationToken cancellationToken = default);
}

public record SearchConsolePerformanceDetail(
    string PropertyUrl,
    int TotalClicks28d,
    int TotalImpressions28d,
    IList<SearchConsoleQueryRow> TopQueries);

public record SearchConsoleQueryRow(string? Query, int Clicks, int Impressions, double Ctr, double Position);

public record SearchConsoleCoverageDetail(
    string PropertyUrl,
    int? IndexedPages,
    int? ExcludedPages,
    IList<SearchConsoleSitemapRow> Sitemaps,
    int InspectedCount,
    int PassedCount,
    int FailedCount);

public record SearchConsoleSitemapRow(string Path, int Errors, int Warnings, bool IsPending);

public record AuditRunDetail(
    AuditRun Run,
    IList<ScannedPage> Pages,
    IList<AuditIssue> Issues);

public record CrawlPagePayload(
    string Url,
    int? StatusCode,
    string? Title,
    string? MetaDescription,
    int CrawlDepth,
    int? ResponseTimeMs,
    IList<CrawlIssuePayload> Issues,
    string? ProgressPhase = null,
    string? ProgressMessage = null);

public record CrawlCompletePayload(int TotalPages, int InternalLinkCount = 0, string? TopLinkedPagesJson = null);

public record CrawlIssuePayload(
    string RuleId,
    string Category,
    AuditIssueSeverity Severity,
    string Message,
    string? Evidence,
    string? FixHint,
    string? DocUrl);

public record AuditExportDto(
    AuditRun Run,
    IList<ScannedPage> Pages,
    IList<AuditIssue> Issues,
    IndexStatusSnapshot? IndexStatus,
    BacklinkSummary? Backlinks,
    IList<PageSpeedResult> PageSpeedResults,
    IList<TrackedKeyword> TrackedKeywords);
