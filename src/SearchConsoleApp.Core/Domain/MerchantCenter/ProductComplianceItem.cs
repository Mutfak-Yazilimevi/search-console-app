using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.MerchantCenter;

public partial class ProductComplianceItem : BaseEntity
{
    public long RunId { get; set; }
    public string PageUrl { get; set; } = "";
    public string? Title { get; set; }
    public string? OfferId { get; set; }
    public string? GmcStatus { get; set; }
    public int ComplianceScore { get; set; }
    public ProductComplianceItemStatus Status { get; set; } = ProductComplianceItemStatus.NonCompliant;
    public string ExtractedDataJson { get; set; } = "{}";
    public int IssueCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
