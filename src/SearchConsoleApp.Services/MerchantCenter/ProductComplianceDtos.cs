using SearchConsoleApp.Core.Domain.MerchantCenter;

namespace SearchConsoleApp.Services.MerchantCenter;

public record ProductComplianceRunSummaryDto(
    Guid EntityId,
    string InputUrl,
    string Status,
    string AnalysisMode,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int? ComplianceScore,
    int TotalProducts);

public record ProductComplianceRunDto(
    Guid EntityId,
    string InputUrl,
    string NormalizedUrl,
    string Status,
    string AnalysisMode,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int TotalProducts,
    int CompliantCount,
    int PartialCount,
    int NonCompliantCount,
    int? ComplianceScore,
    int? SiteReadinessScore,
    int CriticalCount,
    int WarningCount,
    int InfoCount,
    string? ErrorMessage,
    string? ProgressPhase,
    string? ProgressMessage,
    string? MerchantCenterAccountId,
    GmcRunSummaryDto? GmcSummary,
    IList<PriorityActionDto> PriorityActions,
    ProductComplianceComparisonDto? Comparison = null);

public record GmcAggregateStatusDto(string? ReportingContext, long ApprovedCount, long PendingCount, long DisapprovedCount);

public record GmcAccountIssueDto(string Title, string? Detail, string? Severity);

public record GmcRunSummaryDto(
    IList<GmcAggregateStatusDto> AggregateStatuses,
    IList<GmcAccountIssueDto> AccountIssues,
    IList<GmcProductPerformanceDto> TopPerformance);

public record GmcProductPerformanceDto(
    string OfferId,
    string? Title,
    long Clicks,
    long Impressions,
    double? ClickThroughRate);

public record PriorityActionDto(string RuleId, string Message, string FixHint, int AffectedCount);

public record ProductComplianceItemDto(
    Guid EntityId,
    string PageUrl,
    string? Title,
    string? OfferId,
    string? GmcStatus,
    int ComplianceScore,
    string Status,
    int IssueCount);

public record ProductComplianceIssueDto(
    Guid EntityId,
    string? PageUrl,
    string RuleId,
    string Field,
    string Severity,
    string Source,
    string Message,
    string FixHint,
    string? DocUrl,
    string? GmcIssueCode,
    string? Evidence);

public record ProductComplianceDetailDto(
    ProductComplianceRunDto Run,
    IList<ProductComplianceItemDto> Products,
    IList<ProductComplianceIssueDto> SiteIssues,
    IList<ProductComplianceIssueDto> CrossProductIssues,
    IList<ProductComplianceIssueDto> FeedIssues);

public record ProductComplianceProductDetailDto(
    ProductComplianceItemDto Product,
    IList<ProductComplianceIssueDto> Issues);

public record ProductComplianceCrawlProductPayload(string Url, string? Title, string ExtractedProductJson);

public record ProductComplianceCrawlCompletePayload(int TotalProducts, string? SiteCheckHtml);

public interface IProductComplianceService
{
    Task<ProductComplianceRun> StartAsync(string url, long? customerId, string? merchantCenterAccountId, CancellationToken cancellationToken = default);
    Task ProcessProductAsync(Guid runEntityId, ProductComplianceCrawlProductPayload payload, CancellationToken cancellationToken = default);
    Task CompleteCrawlAsync(Guid runEntityId, ProductComplianceCrawlCompletePayload payload, CancellationToken cancellationToken = default);
    Task FailCrawlAsync(Guid runEntityId, string errorMessage, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid runEntityId, CancellationToken cancellationToken = default);
    Task RescanProductAsync(Guid runEntityId, Guid productEntityId, CancellationToken cancellationToken = default);
    Task ProcessProductRescanAsync(
        Guid runEntityId,
        Guid productItemEntityId,
        ProductComplianceCrawlProductPayload payload,
        CancellationToken cancellationToken = default);
    Task CompleteProductRescanAsync(
        Guid runEntityId,
        Guid productItemEntityId,
        CancellationToken cancellationToken = default);
    Task FailProductRescanAsync(Guid runEntityId, string errorMessage, CancellationToken cancellationToken = default);
    Task<ProductComplianceDetailDto?> GetDetailAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<IList<ProductComplianceRunSummaryDto>> ListRecentRunsAsync(
        long customerId,
        int limit = 10,
        CancellationToken cancellationToken = default);
    Task<ProductComplianceDetailDto?> GetExportDetailAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<ProductComplianceProductDetailDto?> GetProductDetailAsync(Guid runEntityId, Guid productEntityId, CancellationToken cancellationToken = default);
    Task<IList<ProductComplianceItemDto>> GetProductsAsync(Guid entityId, int skip, int take, CancellationToken cancellationToken = default);
    Task<string> ExportJsonAsync(Guid entityId, CancellationToken cancellationToken = default);
    Task<string?> ExportHtmlReportAsync(Guid entityId, CancellationToken cancellationToken = default);
}
