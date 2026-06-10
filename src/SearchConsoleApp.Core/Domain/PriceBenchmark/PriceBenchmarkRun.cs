namespace SearchConsoleApp.Core.Domain.PriceBenchmark;

public partial class PriceBenchmarkRun : BaseEntity
{
    public string InputUrl { get; set; } = "";
    public string NormalizedUrl { get; set; } = "";
    public PriceBenchmarkRunStatus Status { get; set; } = PriceBenchmarkRunStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalProducts { get; set; }
    public bool SerpApiConfigured { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProgressPhase { get; set; }
    public string? ProgressMessage { get; set; }
}
