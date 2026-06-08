using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class AuditIssue : BaseEntity
{
    public long AuditRunId { get; set; }
    public string PageUrl { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string Category { get; set; } = "";
    public AuditIssueSeverity Severity { get; set; }
    public AuditIssueSource Source { get; set; } = AuditIssueSource.Crawl;
    public string Message { get; set; } = "";
    public string? Evidence { get; set; }
    public string? FixHint { get; set; }
    public string? DocUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
