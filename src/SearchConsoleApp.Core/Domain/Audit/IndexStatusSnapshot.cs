using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class IndexStatusSnapshot : BaseEntity
{
    public long AuditRunId { get; set; }
    public string Domain { get; set; } = "";
    public int EstimatedIndexedPages { get; set; }
    public int CrawledPages { get; set; }
    public double CoverageRatio { get; set; }
    public string Source { get; set; } = "estimate";
    public string? DetailsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
