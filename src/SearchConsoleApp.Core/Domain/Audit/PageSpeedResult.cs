using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class PageSpeedResult : BaseEntity
{
    public long AuditRunId { get; set; }
    public string Url { get; set; } = "";
    public int PerformanceScore { get; set; }
    public string? Lcp { get; set; }
    public string? Inp { get; set; }
    public string? Cls { get; set; }
    public string Strategy { get; set; } = "mobile";
    public string? DiagnosticsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
