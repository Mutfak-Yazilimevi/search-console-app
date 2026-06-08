namespace SearchConsoleApp.Services.Audit.SearchConsole;

public record SearchConsoleProperty(string SiteUrl, string PermissionLevel);

public record SearchAnalyticsRow(
    string? Query,
    int Clicks,
    int Impressions,
    double Ctr,
    double Position);

public record UrlInspectionResult(
    string Url,
    string Verdict,
    string CoverageState,
    string? RichResultsVerdict,
    string? IndexingState);

public record SearchConsoleAuditData(
    string PropertyUrl,
    IList<SearchAnalyticsRow> TopQueries,
    IList<UrlInspectionResult> UrlInspections,
    int TotalClicks28d,
    int TotalImpressions28d);

public record SearchConsoleSitemapInfo(
    string Path,
    int Errors,
    int Warnings,
    bool IsPending,
    string? LastDownloaded);

public record SearchConsoleCoverageSummary(
    int InspectedCount,
    int PassedCount,
    int FailedCount,
    int SitemapErrorCount,
    int SitemapWarningCount);
