using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class SiteKeywordWatch : BaseEntity
{
    public long CustomerId { get; set; }
    public string SiteHost { get; set; } = "";
    public string Keyword { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
