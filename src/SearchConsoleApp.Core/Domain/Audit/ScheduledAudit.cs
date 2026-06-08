using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class ScheduledAudit : BaseEntity
{
    public long CustomerId { get; set; }
    public string? Label { get; set; }
    public string Url { get; set; } = "";
    public string? SearchConsolePropertyUrl { get; set; }
    public string? MigrationSourceUrl { get; set; }
    public string? Ga4PropertyId { get; set; }
    public int IntervalDays { get; set; } = 7;
    public DateTime NextRunUtc { get; set; }
    public long? LastAuditRunId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool NotifyOnComplete { get; set; } = true;
    public bool NotifyOnCriticalOnly { get; set; }
    public string? WebhookUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
