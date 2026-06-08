using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class KeywordSerpSnapshot : BaseEntity
{
    public long AuditRunId { get; set; }
    public string Keyword { get; set; } = "";
    public int Position { get; set; }
    public string? MatchedUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
