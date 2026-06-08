using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.MerchantCenter;

public partial class ProductComplianceRun : BaseEntity
{
    public string InputUrl { get; set; } = "";
    public string NormalizedUrl { get; set; } = "";
    public ProductComplianceRunStatus Status { get; set; } = ProductComplianceRunStatus.Pending;
    public ProductComplianceAnalysisMode AnalysisMode { get; set; } = ProductComplianceAnalysisMode.SiteOnly;
    public long? CustomerId { get; set; }
    public string? MerchantCenterAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalProducts { get; set; }
    public int CompliantCount { get; set; }
    public int PartialCount { get; set; }
    public int NonCompliantCount { get; set; }
    public int? ComplianceScore { get; set; }
    public int? SiteReadinessScore { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProgressPhase { get; set; }
    public string? ProgressMessage { get; set; }
    public string? GmcSummaryJson { get; set; }
    public string? SiteCheckHtml { get; set; }
    public string? PriorityActionsJson { get; set; }
}
