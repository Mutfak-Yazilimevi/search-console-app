using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class ScannedPage : BaseEntity
{
    public long AuditRunId { get; set; }
    public string Url { get; set; } = "";
    public int? StatusCode { get; set; }
    public string? Title { get; set; }
    public string? MetaDescription { get; set; }
    public int CrawlDepth { get; set; }
    public int? ResponseTimeMs { get; set; }
    public DateTime ScannedAt { get; set; }
}
