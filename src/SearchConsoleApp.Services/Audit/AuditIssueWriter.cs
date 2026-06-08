using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IAuditIssueWriter
{
    Task RecordAsync(AuditRun run, AuditIssue issue, CancellationToken cancellationToken = default);
    void SyncRunCounts(AuditRun run, IList<AuditIssue> issues);
}

public partial class AuditIssueWriter : IAuditIssueWriter, IScopedService
{
    private readonly IRepository<AuditIssue> _issueRepository;

    public AuditIssueWriter(IRepository<AuditIssue> issueRepository)
    {
        _issueRepository = issueRepository;
    }

    public virtual async Task RecordAsync(
        AuditRun run,
        AuditIssue issue,
        CancellationToken cancellationToken = default)
    {
        if (issue.Evidence is { Length: > 16000 })
            issue.Evidence = issue.Evidence[..15997] + "…";

        await _issueRepository.InsertAsync(issue, publishEvent: false);
        IncrementRunCount(run, issue.Severity);
    }

    public virtual void SyncRunCounts(AuditRun run, IList<AuditIssue> issues)
    {
        run.IssuesFound = issues.Count;
        run.CriticalCount = issues.Count(i => i.Severity == AuditIssueSeverity.Critical);
        run.WarningCount = issues.Count(i => i.Severity == AuditIssueSeverity.Warning);
        run.InfoCount = issues.Count(i => i.Severity == AuditIssueSeverity.Info);
    }

    private static void IncrementRunCount(AuditRun run, AuditIssueSeverity severity)
    {
        run.IssuesFound++;
        switch (severity)
        {
            case AuditIssueSeverity.Critical: run.CriticalCount++; break;
            case AuditIssueSeverity.Warning: run.WarningCount++; break;
            default: run.InfoCount++; break;
        }
    }
}
