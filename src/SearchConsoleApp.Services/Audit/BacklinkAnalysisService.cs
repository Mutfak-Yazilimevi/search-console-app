using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IBacklinkAnalysisService
{
    Task AnalyzeInternalLinksAsync(AuditRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// Crawl graph üzerinden dahili link profili özeti.
/// </summary>
public partial class BacklinkAnalysisService : IBacklinkAnalysisService, IScopedService
{
    private readonly IRepository<BacklinkSummary> _summaryRepo;
    private readonly IRepository<ScannedPage> _pageRepo;
    private readonly IRepository<AuditIssue> _issueRepository;
    private readonly IAuditIssueWriter _issueWriter;

    public BacklinkAnalysisService(
        IRepository<BacklinkSummary> summaryRepo,
        IRepository<ScannedPage> pageRepo,
        IRepository<AuditIssue> issueRepository,
        IAuditIssueWriter issueWriter)
    {
        _summaryRepo = summaryRepo;
        _pageRepo = pageRepo;
        _issueRepository = issueRepository;
        _issueWriter = issueWriter;
    }

    public async Task AnalyzeInternalLinksAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        var existing = await _summaryRepo.Table
            .FirstOrDefaultAsync(b => b.AuditRunId == run.Id, cancellationToken);
        if (existing != null)
        {
            await MaybeEmitOrphanIssueAsync(run, existing.OrphanPageCount, await _pageRepo.Table
                .CountAsync(p => p.AuditRunId == run.Id, cancellationToken), cancellationToken);
            return;
        }

        var pages = await _pageRepo.Table
            .Where(p => p.AuditRunId == run.Id)
            .Select(p => p.Url)
            .ToListAsync(cancellationToken);

        if (pages.Count == 0) return;

        var orphanIssues = await _issueRepository.Table
            .CountAsync(i => i.AuditRunId == run.Id && i.RuleId == "orphan-page", cancellationToken);

        await _summaryRepo.InsertAsync(new BacklinkSummary
        {
            AuditRunId = run.Id,
            InternalLinkCount = 0,
            UniqueInternalTargets = pages.Count,
            OrphanPageCount = orphanIssues,
            TopLinkedPagesJson = JsonSerializer.Serialize(pages.Take(10)),
            CreatedAtUtc = DateTime.UtcNow,
        }, publishEvent: false);

        await MaybeEmitOrphanIssueAsync(run, orphanIssues, pages.Count, cancellationToken);
    }

    private async Task MaybeEmitOrphanIssueAsync(
        AuditRun run, int orphanIssues, int pageCount, CancellationToken cancellationToken)
    {
        if (orphanIssues <= pageCount * 0.2 || pageCount < 5) return;

        var hasIssue = await _issueRepository.Table.AnyAsync(
            i => i.AuditRunId == run.Id && i.RuleId == "LINK-EXT-001", cancellationToken);
        if (hasIssue) return;

        await _issueWriter.RecordAsync(run, new AuditIssue
        {
            AuditRunId = run.Id,
            PageUrl = run.NormalizedUrl,
            RuleId = "LINK-EXT-001",
            Category = "backlinks",
            Severity = AuditIssueSeverity.Info,
            Source = AuditIssueSource.Crawl,
            Message = $"{orphanIssues} yetim sayfa — dahili link ağı zayıf.",
            Evidence = IssueDetailEvidenceBuilder.Build(
                $"{orphanIssues} yetim sayfa",
                [
                    new() { Label = "Taranan sayfa", Value = pageCount.ToString() },
                    new() { Label = "Yetim sayfa", Value = orphanIssues.ToString() },
                    new() { Label = "Ne yapmalı", Value = "Site içi navigasyon ve ilgili sayfalardan link verin" },
                ]),
            FixHint = "Navigasyon ve içerik bağlantılarını güçlendirin.",
            CreatedAt = DateTime.UtcNow,
        }, cancellationToken);
    }
}
