using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.MerchantCenter;

public record ProductComplianceComparisonDto(
    Guid PreviousRunEntityId,
    DateTime? PreviousCompletedAt,
    int? PreviousComplianceScore,
    int ComplianceScoreDelta,
    IList<string> NewCriticalRuleIds,
    IList<string> ResolvedCriticalRuleIds);

public interface IGmcComplianceDiffService
{
    Task<ProductComplianceComparisonDto?> BuildComparisonAsync(
        ProductComplianceRun run,
        CancellationToken cancellationToken = default);
}

public class GmcComplianceDiffService : IGmcComplianceDiffService, IScopedService
{
    private readonly IRepository<ProductComplianceRun> _runRepo;
    private readonly IRepository<ProductComplianceIssue> _issueRepo;

    public GmcComplianceDiffService(
        IRepository<ProductComplianceRun> runRepo,
        IRepository<ProductComplianceIssue> issueRepo)
    {
        _runRepo = runRepo;
        _issueRepo = issueRepo;
    }

    public async Task<ProductComplianceComparisonDto?> BuildComparisonAsync(
        ProductComplianceRun run,
        CancellationToken cancellationToken = default)
    {
        if (run.Status != ProductComplianceRunStatus.Completed) return null;

        var previous = await _runRepo.Table
            .Where(r => r.Id != run.Id
                && r.NormalizedUrl == run.NormalizedUrl
                && r.CustomerId == run.CustomerId
                && r.Status == ProductComplianceRunStatus.Completed)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (previous == null) return null;

        var delta = (run.ComplianceScore ?? 0) - (previous.ComplianceScore ?? 0);

        var prevCritical = await _issueRepo.Table
            .Where(i => i.RunId == previous.Id && i.Severity == ProductComplianceIssueSeverity.Critical)
            .Select(i => i.RuleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var currCritical = await _issueRepo.Table
            .Where(i => i.RunId == run.Id && i.Severity == ProductComplianceIssueSeverity.Critical)
            .Select(i => i.RuleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new ProductComplianceComparisonDto(
            previous.EntityId,
            previous.CompletedAt,
            previous.ComplianceScore,
            delta,
            currCritical.Except(prevCritical).Take(10).ToList(),
            prevCritical.Except(currCritical).Take(10).ToList());
    }
}
