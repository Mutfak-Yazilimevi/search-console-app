using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class AuditRun : BaseEntity
{
    public string InputUrl { get; set; } = "";
    public string NormalizedUrl { get; set; } = "";
    public AuditRunStatus Status { get; set; } = AuditRunStatus.Pending;
    public AuditMode Mode { get; set; } = AuditMode.Anonymous;
    public long? CustomerId { get; set; }
    public long? ScheduledAuditId { get; set; }
    public string? SearchConsolePropertyUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int PagesCrawled { get; set; }
    public int IssuesFound { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public int? Score { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProgressPhase { get; set; }
    public string? ProgressMessage { get; set; }
}
