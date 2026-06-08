using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class SearchConsoleSnapshot : BaseEntity
{
    public long AuditRunId { get; set; }
    public string PropertyUrl { get; set; } = "";
    public string PerformanceJson { get; set; } = "{}";
    public string? SitemapsJson { get; set; }
    public int? IndexedPages { get; set; }
    public int? ExcludedPages { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
