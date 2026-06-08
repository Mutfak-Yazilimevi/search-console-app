using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface ISecurityScoreService
{
    Task EvaluateAsync(AuditRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe Browsing + güvenlik kategorisi sorunlarını birleşik skor olarak raporlar.
/// </summary>
public partial class SecurityScoreService : ISecurityScoreService, IScopedService
{
    private readonly IRepository<AuditIssue> _issueRepository;
    private readonly IAuditIssueWriter _issueWriter;
    private readonly ILogger<SecurityScoreService> _logger;

    public SecurityScoreService(
        IRepository<AuditIssue> issueRepository,
        IAuditIssueWriter issueWriter,
        ILogger<SecurityScoreService> logger)
    {
        _issueRepository = issueRepository;
        _issueWriter = issueWriter;
        _logger = logger;
    }

    public async Task EvaluateAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        var securityIssues = await _issueRepository.Table
            .Where(i => i.AuditRunId == run.Id
                && (i.Category == "security"
                    || i.Source == AuditIssueSource.SafeBrowsing
                    || i.RuleId.StartsWith("SAFE-")))
            .ToListAsync(cancellationToken);

        var httpsIssue = await _issueRepository.Table.AnyAsync(i =>
            i.AuditRunId == run.Id
            && (i.RuleId == "https-required"
                || (i.Message != null && i.Message.ToLower().Contains("https"))),
            cancellationToken);

        var score = 100;
        score -= securityIssues.Count(i => i.Severity == AuditIssueSeverity.Critical) * 40;
        score -= securityIssues.Count(i => i.Severity == AuditIssueSeverity.Warning) * 15;
        if (httpsIssue) score -= 20;
        score = Math.Clamp(score, 0, 100);

        if (score >= 80) return;

        await _issueWriter.RecordAsync(run, new AuditIssue
        {
            AuditRunId = run.Id,
            PageUrl = run.NormalizedUrl,
            RuleId = "SEC-001",
            Category = "security",
            Severity = score < 50 ? AuditIssueSeverity.Critical : AuditIssueSeverity.Warning,
            Source = AuditIssueSource.System,
            Message = $"Birleşik güvenlik skoru düşük: {score}/100 ({securityIssues.Count} güvenlik uyarısı).",
            Evidence = string.Join(", ", securityIssues.Take(5).Select(i => i.RuleId).Distinct()),
            FixHint = "Safe Browsing uyarıları, HTTPS ve Search Console güvenlik bildirimlerini giderin.",
            DocUrl = "https://developers.google.com/search/docs/monitor-debug/security",
            CreatedAt = DateTime.UtcNow,
        }, cancellationToken);

        _logger.LogInformation("Security score for {EntityId}: {Score}", run.EntityId, score);
    }
}
