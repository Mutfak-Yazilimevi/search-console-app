using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IAuditDiffService
{
    Task CompareWithPreviousRunAsync(AuditRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// Zamanlanmış denetimlerde önceki koşu ile skor/sorun farkını raporlar.
/// </summary>
public partial class AuditDiffService : IAuditDiffService, IScopedService
{
    private readonly IRepository<AuditRun> _auditRunRepository;
    private readonly IRepository<AuditIssue> _issueRepository;
    private readonly IAuditIssueWriter _issueWriter;
    private readonly ILogger<AuditDiffService> _logger;

    public AuditDiffService(
        IRepository<AuditRun> auditRunRepository,
        IRepository<AuditIssue> issueRepository,
        IAuditIssueWriter issueWriter,
        ILogger<AuditDiffService> logger)
    {
        _auditRunRepository = auditRunRepository;
        _issueRepository = issueRepository;
        _issueWriter = issueWriter;
        _logger = logger;
    }

    public async Task CompareWithPreviousRunAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        AuditRun? previous;

        if (run.ScheduledAuditId.HasValue)
        {
            previous = await _auditRunRepository.Table
                .Where(r => r.ScheduledAuditId == run.ScheduledAuditId
                    && r.Id != run.Id
                    && r.Status == AuditRunStatus.Completed)
                .OrderByDescending(r => r.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            var host = new Uri(run.NormalizedUrl).Host;
            previous = await _auditRunRepository.Table
                .Where(r => r.Id != run.Id
                    && r.CustomerId == run.CustomerId
                    && r.Status == AuditRunStatus.Completed
                    && r.NormalizedUrl.Contains(host))
                .OrderByDescending(r => r.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (previous == null) return;

        var scoreDelta = (run.Score ?? 0) - (previous.Score ?? 0);
        if (scoreDelta <= -10)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = run.NormalizedUrl,
                RuleId = "SCHED-001",
                Category = "monitoring",
                Severity = AuditIssueSeverity.Warning,
                Source = AuditIssueSource.System,
                Message = $"SEO skoru düştü: {previous.Score} → {run.Score} ({scoreDelta}).",
                FixHint = "Yeni kritik/uyarı sorunlarını inceleyin; son deploy veya içerik değişikliklerini kontrol edin.",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }
        else if (scoreDelta >= 10)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = run.NormalizedUrl,
                RuleId = "SCHED-003",
                Category = "monitoring",
                Severity = AuditIssueSeverity.Info,
                Source = AuditIssueSource.System,
                Message = $"SEO skoru iyileşti: {previous.Score} → {run.Score} (+{scoreDelta}).",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }

        var prevRules = await _issueRepository.Table
            .Where(i => i.AuditRunId == previous.Id && i.Severity == AuditIssueSeverity.Critical)
            .Select(i => i.RuleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var currRules = await _issueRepository.Table
            .Where(i => i.AuditRunId == run.Id && i.Severity == AuditIssueSeverity.Critical)
            .Select(i => i.RuleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var newCritical = currRules.Except(prevRules).Take(5).ToList();
        foreach (var ruleId in newCritical)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = run.NormalizedUrl,
                RuleId = "SCHED-002",
                Category = "monitoring",
                Severity = AuditIssueSeverity.Warning,
                Source = AuditIssueSource.System,
                Message = $"Yeni kritik sorun (önceki taramada yoktu): {ruleId}.",
                FixHint = "Bu kural için bulunan sorunları giderin.",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Audit diff for {EntityId}: score {Prev}→{Curr}, new critical rules={Count}",
            run.EntityId, previous.Score, run.Score, newCritical.Count);
    }
}
