using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public partial class AuditService : IAuditService, IScopedService
{
    private readonly IRepository<AuditRun> _auditRunRepository;
    private readonly IRepository<ScannedPage> _scannedPageRepository;
    private readonly IRepository<AuditIssue> _auditIssueRepository;
    private readonly IRepository<SearchConsoleSnapshot> _snapshotRepository;
    private readonly IRepository<ContentQualityScore> _contentQualityRepository;
    private readonly IRepository<PageSpeedResult> _pageSpeedRepository;
    private readonly IRepository<IndexStatusSnapshot> _indexStatusRepository;
    private readonly IRepository<BacklinkSummary> _backlinkRepository;
    private readonly IRepository<TrackedKeyword> _trackedKeywordRepository;
    private readonly IRepository<KeywordSerpSnapshot> _keywordSerpRepository;
    private readonly IExternalAuditService _externalAuditService;
    private readonly IAuditDiffService _auditDiffService;
    private readonly IAuditNotificationService _auditNotificationService;
    private readonly IAuditReportService _auditReportService;
    private readonly IAuditIssueWriter _issueWriter;
    private readonly IAuditQuotaService _auditQuotaService;
    private readonly ICrawlWorkerClient _crawlWorkerClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IRepository<AuditRun> auditRunRepository,
        IRepository<ScannedPage> scannedPageRepository,
        IRepository<AuditIssue> auditIssueRepository,
        IRepository<SearchConsoleSnapshot> snapshotRepository,
        IRepository<ContentQualityScore> contentQualityRepository,
        IRepository<PageSpeedResult> pageSpeedRepository,
        IRepository<IndexStatusSnapshot> indexStatusRepository,
        IRepository<BacklinkSummary> backlinkRepository,
        IRepository<TrackedKeyword> trackedKeywordRepository,
        IRepository<KeywordSerpSnapshot> keywordSerpRepository,
        IExternalAuditService externalAuditService,
        IAuditDiffService auditDiffService,
        IAuditNotificationService auditNotificationService,
        IAuditReportService auditReportService,
        IAuditIssueWriter issueWriter,
        IAuditQuotaService auditQuotaService,
        ICrawlWorkerClient crawlWorkerClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<AuditService> logger)
    {
        _auditRunRepository = auditRunRepository;
        _scannedPageRepository = scannedPageRepository;
        _auditIssueRepository = auditIssueRepository;
        _snapshotRepository = snapshotRepository;
        _contentQualityRepository = contentQualityRepository;
        _pageSpeedRepository = pageSpeedRepository;
        _indexStatusRepository = indexStatusRepository;
        _backlinkRepository = backlinkRepository;
        _trackedKeywordRepository = trackedKeywordRepository;
        _keywordSerpRepository = keywordSerpRepository;
        _externalAuditService = externalAuditService;
        _auditDiffService = auditDiffService;
        _auditNotificationService = auditNotificationService;
        _auditReportService = auditReportService;
        _issueWriter = issueWriter;
        _auditQuotaService = auditQuotaService;
        _crawlWorkerClient = crawlWorkerClient;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public virtual async Task<AuditRun> StartAuditAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required.", nameof(url));

        await EnsureQuotaAsync(null, cancellationToken);

        var normalized = AuditUrlNormalizer.Normalize(url);
        var now = DateTime.UtcNow;

        var run = new AuditRun
        {
            InputUrl = url.Trim(),
            NormalizedUrl = normalized,
            Status = AuditRunStatus.Pending,
            Mode = AuditMode.Anonymous,
            CreatedAt = now,
        };

        await _auditRunRepository.InsertAsync(run, publishEvent: false);
        await EnqueueCrawlJobAsync(run, cancellationToken);
        return run;
    }

    public virtual async Task<AuditRun> StartScheduledAuditAsync(
        string url,
        long customerId,
        long scheduledAuditId,
        string? searchConsolePropertyUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required.", nameof(url));

        await EnsureQuotaAsync(customerId, cancellationToken);

        var normalized = AuditUrlNormalizer.Normalize(url);
        var now = DateTime.UtcNow;

        var run = new AuditRun
        {
            InputUrl = url.Trim(),
            NormalizedUrl = normalized,
            Status = AuditRunStatus.Pending,
            Mode = AuditMode.Connected,
            CustomerId = customerId,
            ScheduledAuditId = scheduledAuditId,
            SearchConsolePropertyUrl = searchConsolePropertyUrl,
            CreatedAt = now,
        };

        await _auditRunRepository.InsertAsync(run, publishEvent: false);
        await EnqueueCrawlJobAsync(run, cancellationToken);
        return run;
    }

    public virtual async Task<AuditRun> StartConnectedAuditAsync(
        string url,
        long customerId,
        string? searchConsolePropertyUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required.", nameof(url));

        await EnsureQuotaAsync(customerId, cancellationToken);

        var normalized = AuditUrlNormalizer.Normalize(url);
        var now = DateTime.UtcNow;

        var run = new AuditRun
        {
            InputUrl = url.Trim(),
            NormalizedUrl = normalized,
            Status = AuditRunStatus.Pending,
            Mode = AuditMode.Connected,
            CustomerId = customerId,
            SearchConsolePropertyUrl = searchConsolePropertyUrl,
            CreatedAt = now,
        };

        await _auditRunRepository.InsertAsync(run, publishEvent: false);
        await EnqueueCrawlJobAsync(run, cancellationToken);
        return run;
    }

    public virtual async Task<SearchConsolePerformanceDetail?> GetPerformanceAsync(
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(entityId);
        if (run == null) return null;

        var snapshot = await _snapshotRepository.Table
            .FirstOrDefaultAsync(s => s.AuditRunId == run.Id, cancellationToken);
        if (snapshot == null) return null;

        using var doc = JsonDocument.Parse(snapshot.PerformanceJson);
        var root = doc.RootElement;
        var clicks = root.TryGetProperty("TotalClicks28d", out var c) ? c.GetInt32() : 0;
        var impressions = root.TryGetProperty("TotalImpressions28d", out var i) ? i.GetInt32() : 0;

        var queries = new List<SearchConsoleQueryRow>();
        if (root.TryGetProperty("topQueries", out var tq))
        {
            foreach (var row in tq.EnumerateArray())
            {
                queries.Add(new SearchConsoleQueryRow(
                    row.TryGetProperty("Query", out var q) ? q.GetString() : null,
                    row.TryGetProperty("Clicks", out var cl) ? cl.GetInt32() : 0,
                    row.TryGetProperty("Impressions", out var im) ? im.GetInt32() : 0,
                    row.TryGetProperty("Ctr", out var ct) ? ct.GetDouble() : 0,
                    row.TryGetProperty("Position", out var p) ? p.GetDouble() : 0));
            }
        }

        return new SearchConsolePerformanceDetail(snapshot.PropertyUrl, clicks, impressions, queries);
    }

    public virtual async Task<IList<ContentQualityScore>> GetContentQualityAsync(
        Guid entityId,
        CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(entityId);
        if (run == null) return [];

        return await _contentQualityRepository.Table
            .Where(c => c.AuditRunId == run.Id)
            .OrderByDescending(c => c.EeatScore)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<IList<PageSpeedResult>> GetPageSpeedAsync(
        Guid entityId, CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(entityId);
        if (run == null) return [];
        return await _pageSpeedRepository.Table
            .Where(p => p.AuditRunId == run.Id)
            .OrderBy(p => p.Url)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<IndexStatusSnapshot?> GetIndexStatusAsync(
        Guid entityId, CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(entityId);
        if (run == null) return null;
        return await _indexStatusRepository.Table
            .FirstOrDefaultAsync(s => s.AuditRunId == run.Id, cancellationToken);
    }

    public virtual async Task<BacklinkSummary?> GetBacklinksAsync(
        Guid entityId, CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(entityId);
        if (run == null) return null;
        return await _backlinkRepository.Table
            .FirstOrDefaultAsync(b => b.AuditRunId == run.Id, cancellationToken);
    }

    public virtual async Task<IList<TrackedKeyword>> GetTrackedKeywordsAsync(
        Guid entityId, CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(entityId);
        if (run == null) return [];
        return await _trackedKeywordRepository.Table
            .Where(k => k.AuditRunId == run.Id)
            .OrderByDescending(k => k.Impressions)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<IList<KeywordSerpSnapshot>> GetKeywordSerpAsync(
        Guid entityId, CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(entityId);
        if (run == null) return [];
        return await _keywordSerpRepository.Table
            .Where(s => s.AuditRunId == run.Id)
            .OrderBy(s => s.Position == 0 ? 999 : s.Position)
            .ThenBy(s => s.Keyword)
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<SearchConsoleCoverageDetail?> GetSearchConsoleCoverageAsync(
        Guid entityId, CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(entityId);
        if (run == null) return null;

        var snapshot = await _snapshotRepository.Table
            .FirstOrDefaultAsync(s => s.AuditRunId == run.Id, cancellationToken);
        if (snapshot == null) return null;

        var sitemaps = new List<SearchConsoleSitemapRow>();
        var inspected = 0;
        var passed = snapshot.IndexedPages ?? 0;
        var failed = snapshot.ExcludedPages ?? 0;

        if (!string.IsNullOrWhiteSpace(snapshot.SitemapsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(snapshot.SitemapsJson);
                if (doc.RootElement.TryGetProperty("inspectionSummary", out var summary))
                {
                    inspected = summary.TryGetProperty("inspected", out var i) ? i.GetInt32() : 0;
                    if (summary.TryGetProperty("passed", out var p)) passed = p.GetInt32();
                    if (summary.TryGetProperty("failed", out var f)) failed = f.GetInt32();
                }
                if (doc.RootElement.TryGetProperty("sitemaps", out var sm) && sm.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in sm.EnumerateArray())
                    {
                        sitemaps.Add(new SearchConsoleSitemapRow(
                            item.TryGetProperty("Path", out var path) ? path.GetString() ?? "" : "",
                            item.TryGetProperty("Errors", out var err) ? err.GetInt32() : 0,
                            item.TryGetProperty("Warnings", out var warn) ? warn.GetInt32() : 0,
                            item.TryGetProperty("IsPending", out var pend) && pend.GetBoolean()));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse SitemapsJson for audit {EntityId}", entityId);
            }
        }

        return new SearchConsoleCoverageDetail(
            snapshot.PropertyUrl,
            snapshot.IndexedPages,
            snapshot.ExcludedPages,
            sitemaps,
            inspected,
            passed,
            failed);
    }

    public virtual async Task<AuditExportDto?> ExportAuditAsync(
        Guid entityId, CancellationToken cancellationToken = default)
    {
        var detail = await GetAuditAsync(entityId, cancellationToken);
        if (detail == null) return null;
        var index = await GetIndexStatusAsync(entityId, cancellationToken);
        var backlinks = await GetBacklinksAsync(entityId, cancellationToken);
        var psi = await GetPageSpeedAsync(entityId, cancellationToken);
        var keywords = await GetTrackedKeywordsAsync(entityId, cancellationToken);
        return new AuditExportDto(detail.Run, detail.Pages, detail.Issues, index, backlinks, psi, keywords);
    }

    public virtual Task<string?> ExportHtmlReportAsync(
        Guid entityId, CancellationToken cancellationToken = default)
        => _auditReportService.BuildHtmlReportAsync(entityId, cancellationToken);

    public virtual Task<string?> ExportCriticalHtmlReportAsync(
        Guid entityId, CancellationToken cancellationToken = default)
        => _auditReportService.BuildCriticalHtmlReportAsync(entityId, cancellationToken);

    public virtual async Task<AuditRunDetail?> GetAuditAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(entityId);
        if (run == null) return null;

        var pages = await _scannedPageRepository.Table
            .Where(p => p.AuditRunId == run.Id)
            .OrderBy(p => p.CrawlDepth)
            .ThenBy(p => p.Url)
            .ToListAsync(cancellationToken);

        var issues = await _auditIssueRepository.Table
            .Where(i => i.AuditRunId == run.Id)
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.RuleId)
            .ToListAsync(cancellationToken);

        return new AuditRunDetail(run, pages, issues);
    }

    public virtual async Task EnqueueCrawlJobAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var workerUrl = _config["Audit:CrawlWorkerUrl"];
        if (string.IsNullOrWhiteSpace(workerUrl))
        {
            _logger.LogWarning("Audit:CrawlWorkerUrl is not configured; failing audit {EntityId}", run.EntityId);
            run.Status = AuditRunStatus.Failed;
            run.ErrorMessage = "Crawl worker yapılandırılmamış (Audit:CrawlWorkerUrl). docker compose up -d crawl-worker ile başlatın.";
            run.CompletedAt = DateTime.UtcNow;
            await _auditRunRepository.UpdateAsync(run, publishEvent: false);
            return;
        }

        var maxPages = _config.GetValue("Audit:MaxPages", 50);
        var maxDepth = _config.GetValue("Audit:MaxDepth", 5);
        var payload = new
        {
            auditRunEntityId = run.EntityId,
            url = run.NormalizedUrl,
            maxPages,
            maxDepth,
        };

        var client = _httpClientFactory.CreateClient("audit-crawl");
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{workerUrl.TrimEnd('/')}/enqueue", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Failed to enqueue crawl job for {EntityId}: {Status} {Body}",
                run.EntityId, (int)response.StatusCode, body);

            await FailCrawlAsync(run.EntityId, "Tarama işi kuyruğa eklenemedi.", cancellationToken);
            return;
        }

        run.Status = AuditRunStatus.Crawling;
        run.StartedAt = DateTime.UtcNow;
        await _auditRunRepository.UpdateAsync(run, publishEvent: false);
    }

    public virtual async Task ProcessCrawlPageAsync(
        Guid auditRunEntityId,
        CrawlPagePayload payload,
        CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(auditRunEntityId)
            ?? throw new InvalidOperationException($"Audit run {auditRunEntityId} not found.");

        if (run.Status == AuditRunStatus.Cancelled)
            return;

        if (IsTerminal(run.Status))
            return;

        var now = DateTime.UtcNow;
        var exists = await _scannedPageRepository.Table
            .AnyAsync(p => p.AuditRunId == run.Id && p.Url == payload.Url, cancellationToken);

        if (!exists)
        {
            var page = new ScannedPage
            {
                AuditRunId = run.Id,
                Url = payload.Url,
                StatusCode = payload.StatusCode ?? 0,
                Title = payload.Title,
                MetaDescription = payload.MetaDescription,
                CrawlDepth = payload.CrawlDepth,
                ResponseTimeMs = payload.ResponseTimeMs ?? 0,
                ScannedAt = now,
            };
            await _scannedPageRepository.InsertAsync(page, publishEvent: false);
            run.PagesCrawled++;
        }

        foreach (var i in payload.Issues)
        {
            try
            {
                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = payload.Url,
                    RuleId = i.RuleId,
                    Category = i.Category,
                    Severity = i.Severity,
                    Source = AuditIssueSource.Crawl,
                    Message = i.Message,
                    Evidence = i.Evidence,
                    FixHint = i.FixHint,
                    DocUrl = i.DocUrl,
                    CreatedAt = now,
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping issue {RuleId} for {Url} (evidence save failed)", i.RuleId, payload.Url);
            }
        }

        run.Status = AuditRunStatus.Crawling;
        if (!string.IsNullOrEmpty(payload.ProgressPhase))
            run.ProgressPhase = payload.ProgressPhase;
        if (!string.IsNullOrEmpty(payload.ProgressMessage))
            run.ProgressMessage = payload.ProgressMessage;
        await _auditRunRepository.UpdateAsync(run, publishEvent: false);
    }

    public virtual async Task CompleteCrawlAsync(
        Guid auditRunEntityId,
        CrawlCompletePayload payload,
        CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(auditRunEntityId)
            ?? throw new InvalidOperationException($"Audit run {auditRunEntityId} not found.");

        if (run.Status == AuditRunStatus.Cancelled)
            return;

        if (IsTerminal(run.Status))
            return;

        run.Status = AuditRunStatus.Analyzing;
        await _auditRunRepository.UpdateAsync(run, publishEvent: false);

        if (payload.InternalLinkCount > 0 || !string.IsNullOrEmpty(payload.TopLinkedPagesJson))
        {
            await SaveCrawlLinkStatsAsync(run, payload, cancellationToken);
        }

        await _externalAuditService.RunPostCrawlChecksAsync(run, cancellationToken);

        run = await _auditRunRepository.GetByEntityIdAsync(auditRunEntityId);
        if (run == null || run.Status == AuditRunStatus.Cancelled)
            return;

        var allIssues = await _auditIssueRepository.Table
            .Where(i => i.AuditRunId == run.Id)
            .ToListAsync(cancellationToken);

        run.PagesCrawled = Math.Max(run.PagesCrawled, payload.TotalPages);
        _issueWriter.SyncRunCounts(run, allIssues);
        run.Score = AuditScoreCalculator.CalculateFromIssues(allIssues);

        await _auditDiffService.CompareWithPreviousRunAsync(run, cancellationToken);

        allIssues = await _auditIssueRepository.Table
            .Where(i => i.AuditRunId == run.Id)
            .ToListAsync(cancellationToken);
        _issueWriter.SyncRunCounts(run, allIssues);
        run.Score = AuditScoreCalculator.CalculateFromIssues(allIssues);
        run.Status = AuditRunStatus.Completed;
        run.ProgressPhase = "completed";
        run.ProgressMessage = "Tarama tamamlandı";
        run.CompletedAt = DateTime.UtcNow;
        await _auditRunRepository.UpdateAsync(run, publishEvent: false);

        await _auditNotificationService.NotifyCompletedAsync(run, cancellationToken);
    }

    public virtual async Task FailCrawlAsync(
        Guid auditRunEntityId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(auditRunEntityId);
        if (run == null) return;

        if (run.Status == AuditRunStatus.Cancelled)
            return;

        run.Status = AuditRunStatus.Failed;
        run.ErrorMessage = errorMessage;
        run.CompletedAt = DateTime.UtcNow;
        await _auditRunRepository.UpdateAsync(run, publishEvent: false);

        await _auditNotificationService.NotifyFailedAsync(run, cancellationToken);
    }

    public virtual async Task<AuditRun?> CancelAuditAsync(
        Guid auditRunEntityId,
        CancellationToken cancellationToken = default)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(auditRunEntityId);
        if (run == null) return null;

        if (IsTerminal(run.Status))
            return run;

        run.Status = AuditRunStatus.Cancelled;
        run.ErrorMessage = "Tarama kullanıcı tarafından durduruldu.";
        run.ProgressPhase = "cancelled";
        run.ProgressMessage = "Tarama durduruldu";
        run.CompletedAt = DateTime.UtcNow;
        await _auditRunRepository.UpdateAsync(run, publishEvent: false);

        await _crawlWorkerClient.CancelJobAsync(auditRunEntityId, cancellationToken);
        return run;
    }

    private async Task SaveCrawlLinkStatsAsync(
        AuditRun run, CrawlCompletePayload payload, CancellationToken cancellationToken)
    {
        var existing = await _backlinkRepository.Table
            .FirstOrDefaultAsync(b => b.AuditRunId == run.Id, cancellationToken);
        if (existing != null) return;

        var pages = await _scannedPageRepository.Table
            .CountAsync(p => p.AuditRunId == run.Id, cancellationToken);
        var orphanIssues = await _auditIssueRepository.Table
            .CountAsync(i => i.AuditRunId == run.Id && i.RuleId == "orphan-page", cancellationToken);

        await _backlinkRepository.InsertAsync(new BacklinkSummary
        {
            AuditRunId = run.Id,
            InternalLinkCount = payload.InternalLinkCount,
            UniqueInternalTargets = pages,
            OrphanPageCount = orphanIssues,
            TopLinkedPagesJson = payload.TopLinkedPagesJson ?? "[]",
            CreatedAtUtc = DateTime.UtcNow,
        }, publishEvent: false);
    }

    private static bool IsTerminal(AuditRunStatus status) =>
        status is AuditRunStatus.Completed or AuditRunStatus.Failed or AuditRunStatus.Cancelled;

    private async Task EnsureQuotaAsync(long? customerId, CancellationToken cancellationToken)
    {
        var quota = await _auditQuotaService.ValidateStartAsync(customerId, cancellationToken);
        if (!quota.Allowed)
            throw new AuditQuotaException(quota.Message ?? "Tarama kotası aşıldı.");
    }
}
