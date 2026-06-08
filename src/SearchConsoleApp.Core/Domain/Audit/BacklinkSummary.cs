using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

public partial class BacklinkSummary : BaseEntity
{
    public long AuditRunId { get; set; }
    public int InternalLinkCount { get; set; }
    public int UniqueInternalTargets { get; set; }
    public int OrphanPageCount { get; set; }
    public string? TopLinkedPagesJson { get; set; }
    public int? ExternalReferringDomainCount { get; set; }
    public int? ExternalBacklinkCount { get; set; }
    public string? ExternalTopDomainsJson { get; set; }
    public string? ExternalSource { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
