using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class TrackedKeyword : BaseEntity
{
    public long AuditRunId { get; set; }
    public string Keyword { get; set; } = "";
    public double Position { get; set; }
    public int Impressions { get; set; }
    public int Clicks { get; set; }
    public double Ctr { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
