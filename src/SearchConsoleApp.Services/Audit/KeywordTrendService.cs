using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IKeywordTrendService
{
    Task CompareWithPreviousAuditAsync(AuditRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// Önceki denetimle anahtar kelime pozisyon/gösterim karşılaştırması.
/// </summary>
public partial class KeywordTrendService : IKeywordTrendService, IScopedService
{
    private readonly IRepository<AuditRun> _auditRunRepository;
    private readonly IRepository<TrackedKeyword> _keywordRepository;
    private readonly IAuditIssueWriter _issueWriter;
    private readonly ILogger<KeywordTrendService> _logger;

    public KeywordTrendService(
        IRepository<AuditRun> auditRunRepository,
        IRepository<TrackedKeyword> keywordRepository,
        IAuditIssueWriter issueWriter,
        ILogger<KeywordTrendService> logger)
    {
        _auditRunRepository = auditRunRepository;
        _keywordRepository = keywordRepository;
        _issueWriter = issueWriter;
        _logger = logger;
    }

    public async Task CompareWithPreviousAuditAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        var host = new Uri(run.NormalizedUrl).Host;

        var previousRun = await _auditRunRepository.Table
            .Where(r => r.Id != run.Id
                && r.Status == AuditRunStatus.Completed
                && r.NormalizedUrl.Contains(host))
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousRun == null) return;

        var currentKeywords = await _keywordRepository.Table
            .Where(k => k.AuditRunId == run.Id)
            .ToListAsync(cancellationToken);
        if (currentKeywords.Count == 0) return;

        var previousKeywords = await _keywordRepository.Table
            .Where(k => k.AuditRunId == previousRun.Id)
            .ToDictionaryAsync(k => k.Keyword.ToLowerInvariant(), cancellationToken);

        var declines = 0;
        foreach (var current in currentKeywords)
        {
            if (!previousKeywords.TryGetValue(current.Keyword.ToLowerInvariant(), out var prev)) continue;

            var positionDrop = current.Position - prev.Position >= 5;
            var impressionDrop = prev.Impressions >= 20
                && current.Impressions < prev.Impressions * 0.5;

            if (!positionDrop && !impressionDrop) continue;

            declines++;
            if (declines > 3) break;

            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = run.NormalizedUrl,
                RuleId = "RANK-004",
                Category = "ranking",
                Severity = AuditIssueSeverity.Info,
                Source = AuditIssueSource.SearchConsole,
                Message = $"Anahtar kelime düşüşü: \"{current.Keyword}\" (pozisyon {prev.Position:F1}→{current.Position:F1}, gösterim {prev.Impressions}→{current.Impressions}).",
                FixHint = "İlgili sorgu için içerik ve snippet'i güçlendirin.",
                DocUrl = "https://developers.google.com/search/docs/monitor-debug/search-console-start",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }
    }
}
