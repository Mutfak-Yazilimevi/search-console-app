using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.MerchantCenter;

public partial class ProductComplianceIssue : BaseEntity
{
    public long RunId { get; set; }
    public long? ItemId { get; set; }
    public string? PageUrl { get; set; }
    public string RuleId { get; set; } = "";
    public string Field { get; set; } = "";
    public ProductComplianceIssueSeverity Severity { get; set; }
    public ProductComplianceIssueSource Source { get; set; }
    public string Message { get; set; } = "";
    public string FixHint { get; set; } = "";
    public string? DocUrl { get; set; }
    public string? GmcIssueCode { get; set; }
    public string? Evidence { get; set; }
    public DateTime CreatedAt { get; set; }
}
