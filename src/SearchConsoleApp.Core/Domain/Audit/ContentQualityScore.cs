using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class ContentQualityScore : BaseEntity
{
    public long AuditRunId { get; set; }
    public string Url { get; set; } = "";
    public int EeatScore { get; set; }
    public string ChecklistJson { get; set; } = "[]";
    public string? SuggestionsJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
