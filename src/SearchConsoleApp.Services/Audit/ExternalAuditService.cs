using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Audit.SearchConsole;

namespace SearchConsoleApp.Services.Audit;

/// <summary>
/// Post-crawl external checks: PageSpeed, Safe Browsing, Search Console, LLM content quality.
/// </summary>
public interface IExternalAuditService
{
    Task RunPostCrawlChecksAsync(AuditRun run, CancellationToken cancellationToken = default);
}

public partial class ExternalAuditService : IExternalAuditService, IScopedService
{
    private readonly IAuditIssueWriter _issueWriter;
    private readonly IRepository<ScannedPage> _pageRepository;
    private readonly IRepository<SearchConsoleSnapshot> _snapshotRepository;
    private readonly IRepository<PageSpeedResult> _pageSpeedRepository;
    private readonly IRepository<TrackedKeyword> _trackedKeywordRepository;
    private readonly ISearchConsoleAuthService _scAuth;
    private readonly ISearchConsoleApiClient _scApi;
    private readonly ISearchConsoleCoverageService _scCoverage;
    private readonly IKeywordTrendService _keywordTrend;
    private readonly IDiscoverImageCheckService _discoverImageCheck;
    private readonly IContentQualityService _contentQuality;
    private readonly IIndexCheckService _indexCheck;
    private readonly IRichResultsCheckService _richResultsCheck;
    private readonly IBacklinkAnalysisService _backlinkAnalysis;
    private readonly IExternalBacklinkService _externalBacklink;
    private readonly IKeywordSerpCheckService _keywordSerp;
    private readonly IMigrationCheckService _migrationCheck;
    private readonly IGa4AnalyticsService _ga4Analytics;
    private readonly ISecurityScoreService _securityScore;
    private readonly IRepository<ScheduledAudit> _scheduledAuditRepository;
    private readonly IRepository<AuditRun> _auditRunRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IIntegrationSettingsService _integrationSettings;
    private readonly ILogger<ExternalAuditService> _logger;

    public ExternalAuditService(
        IAuditIssueWriter issueWriter,
        IRepository<ScannedPage> pageRepository,
        IRepository<SearchConsoleSnapshot> snapshotRepository,
        IRepository<PageSpeedResult> pageSpeedRepository,
        IRepository<TrackedKeyword> trackedKeywordRepository,
        ISearchConsoleAuthService scAuth,
        ISearchConsoleApiClient scApi,
        ISearchConsoleCoverageService scCoverage,
        IKeywordTrendService keywordTrend,
        IDiscoverImageCheckService discoverImageCheck,
        IContentQualityService contentQuality,
        IIndexCheckService indexCheck,
        IRichResultsCheckService richResultsCheck,
        IBacklinkAnalysisService backlinkAnalysis,
        IExternalBacklinkService externalBacklink,
        IKeywordSerpCheckService keywordSerp,
        IMigrationCheckService migrationCheck,
        IGa4AnalyticsService ga4Analytics,
        ISecurityScoreService securityScore,
        IRepository<ScheduledAudit> scheduledAuditRepository,
        IRepository<AuditRun> auditRunRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IIntegrationSettingsService integrationSettings,
        ILogger<ExternalAuditService> logger)
    {
        _issueWriter = issueWriter;
        _pageRepository = pageRepository;
        _snapshotRepository = snapshotRepository;
        _pageSpeedRepository = pageSpeedRepository;
        _trackedKeywordRepository = trackedKeywordRepository;
        _scAuth = scAuth;
        _scApi = scApi;
        _scCoverage = scCoverage;
        _keywordTrend = keywordTrend;
        _discoverImageCheck = discoverImageCheck;
        _contentQuality = contentQuality;
        _indexCheck = indexCheck;
        _richResultsCheck = richResultsCheck;
        _backlinkAnalysis = backlinkAnalysis;
        _externalBacklink = externalBacklink;
        _keywordSerp = keywordSerp;
        _migrationCheck = migrationCheck;
        _ga4Analytics = ga4Analytics;
        _securityScore = securityScore;
        _scheduledAuditRepository = scheduledAuditRepository;
        _auditRunRepository = auditRunRepository;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _integrationSettings = integrationSettings;
        _logger = logger;
    }

    public virtual async Task RunPostCrawlChecksAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        run.ProgressPhase = "analyzing";
        run.ProgressMessage = "Safe Browsing kontrol ediliyor…";
        if (_integrationSettings.IsEnabled("safe-browsing"))
            await CheckSafeBrowsingAsync(run, cancellationToken);
        if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;

        run.ProgressMessage = "Site taşıma ve domain kontrolü…";
        await RunMigrationChecksAsync(run, cancellationToken);
        if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;

        run.ProgressMessage = "PageSpeed Insights çalışıyor…";
        if (_integrationSettings.IsEnabled("pagespeed"))
            await CheckPageSpeedAsync(run, cancellationToken);
        if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;

        run.ProgressMessage = "İndeks durumu kontrol ediliyor…";
        if (_integrationSettings.IsEnabled("custom-search"))
            await _indexCheck.CheckIndexAsync(run, cancellationToken);
        if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;

        run.ProgressMessage = "Zengin sonuç (JSON-LD) doğrulanıyor…";
        await _richResultsCheck.CheckRichResultsAsync(run, cancellationToken);
        if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;

        run.ProgressMessage = "Discover og:image boyutları kontrol ediliyor…";
        await _discoverImageCheck.CheckDiscoverImagesAsync(run, cancellationToken);
        if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;

        run.ProgressMessage = "Dahili link analizi…";
        await _backlinkAnalysis.AnalyzeInternalLinksAsync(run, cancellationToken);
        if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;

        run.ProgressMessage = "Harici backlink profili…";
        if (_integrationSettings.IsEnabled("backlinks-ahrefs") || _integrationSettings.IsEnabled("backlinks-moz"))
            await _externalBacklink.FetchExternalBacklinksAsync(run, cancellationToken);
        if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;

        if (run.Mode == AuditMode.Connected && run.CustomerId.HasValue && _integrationSettings.IsEnabled("custom-search"))
        {
            run.ProgressMessage = "Anahtar kelime SERP kontrolü…";
            await _keywordSerp.CheckKeywordsAsync(run, cancellationToken);
            if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;
        }

        if (run.Mode == AuditMode.Connected && run.CustomerId.HasValue && _integrationSettings.IsEnabled("search-console"))
        {
            run.ProgressMessage = "Search Console verisi alınıyor…";
            await CheckSearchConsoleAsync(run, cancellationToken);
            if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;

            run.ProgressMessage = "GA4 / trafik trendi…";
            await RunGa4ChecksAsync(run, cancellationToken);
            if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;
        }

        run.ProgressMessage = "İçerik kalitesi (E-E-A-T) analizi…";
        if (_integrationSettings.IsEnabled("llm-eeat"))
            await _contentQuality.AnalyzeTopPagesAsync(run, cancellationToken);
        if (await IsRunCancelledAsync(run.Id, cancellationToken)) return;

        run.ProgressMessage = "Güvenlik skoru güncelleniyor…";
        await _securityScore.EvaluateAsync(run, cancellationToken);
    }

    private async Task<bool> IsRunCancelledAsync(long auditRunId, CancellationToken cancellationToken)
    {
        var status = await _auditRunRepository.Table
            .Where(r => r.Id == auditRunId)
            .Select(r => r.Status)
            .FirstOrDefaultAsync(cancellationToken);
        return status == AuditRunStatus.Cancelled;
    }

    private async Task RunMigrationChecksAsync(AuditRun run, CancellationToken cancellationToken)
    {
        string? migrationUrl = null;
        if (run.ScheduledAuditId.HasValue)
        {
            var schedule = await _scheduledAuditRepository.Table
                .FirstOrDefaultAsync(s => s.Id == run.ScheduledAuditId.Value, cancellationToken);
            migrationUrl = schedule?.MigrationSourceUrl;
        }

        await _migrationCheck.CheckMigrationAsync(run, migrationUrl, cancellationToken);
    }

    private async Task RunGa4ChecksAsync(AuditRun run, CancellationToken cancellationToken)
    {
        string? ga4PropertyId = null;
        if (run.ScheduledAuditId.HasValue)
        {
            var schedule = await _scheduledAuditRepository.Table
                .FirstOrDefaultAsync(s => s.Id == run.ScheduledAuditId.Value, cancellationToken);
            ga4PropertyId = schedule?.Ga4PropertyId;
        }

        await _ga4Analytics.CheckTrafficTrendAsync(run, ga4PropertyId, cancellationToken);
    }

    private async Task CheckSearchConsoleAsync(AuditRun run, CancellationToken cancellationToken)
    {
        var accessToken = await _scAuth.GetAccessTokenAsync(run.CustomerId!.Value, cancellationToken);
        if (accessToken == null)
        {
            _logger.LogWarning("SC token unavailable for audit {EntityId}", run.EntityId);
            return;
        }

        var properties = await _scApi.ListPropertiesAsync(accessToken, cancellationToken);
        var propertyUrl = run.SearchConsolePropertyUrl
            ?? _scApi.FindMatchingProperty(properties, run.NormalizedUrl);

        if (propertyUrl == null)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = run.NormalizedUrl,
                RuleId = "SC-001",
                Category = "search-console",
                Severity = AuditIssueSeverity.Warning,
                Source = AuditIssueSource.SearchConsole,
                Message = "Bu URL için eşleşen Search Console mülkü bulunamadı.",
                FixHint = "Siteyi Google Search Console'da doğrulayın ve Google hesabınızın erişimi olduğundan emin olun.",
                DocUrl = "https://developers.google.com/search/docs/monitor-debug/search-console-start",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
            return;
        }

        var pages = await _pageRepository.Table
            .Where(p => p.AuditRunId == run.Id)
            .OrderBy(p => p.CrawlDepth)
            .Select(p => p.Url)
            .ToListAsync(cancellationToken);

        var scData = await _scApi.FetchAuditDataAsync(accessToken, propertyUrl, pages, cancellationToken);
        if (scData == null) return;

        await _snapshotRepository.InsertAsync(new SearchConsoleSnapshot
        {
            AuditRunId = run.Id,
            PropertyUrl = propertyUrl,
            PerformanceJson = JsonSerializer.Serialize(new
            {
                scData.TotalClicks28d,
                scData.TotalImpressions28d,
                topQueries = scData.TopQueries,
            }),
            CreatedAtUtc = DateTime.UtcNow,
        }, publishEvent: false);

        foreach (var q in scData.TopQueries.Take(20))
        {
            if (string.IsNullOrWhiteSpace(q.Query)) continue;
            await _trackedKeywordRepository.InsertAsync(new TrackedKeyword
            {
                AuditRunId = run.Id,
                Keyword = q.Query,
                Position = q.Position,
                Impressions = q.Impressions,
                Clicks = q.Clicks,
                Ctr = q.Ctr,
                CreatedAtUtc = DateTime.UtcNow,
            }, publishEvent: false);
        }

        if (scData.TotalImpressions28d == 0 && pages.Count > 3)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = run.NormalizedUrl,
                RuleId = "RANK-001",
                Category = "ranking",
                Severity = AuditIssueSeverity.Info,
                Source = AuditIssueSource.SearchConsole,
                Message = "Son 28 günde arama gösterimi yok (Search Console verisi).",
                FixHint = "İndeks durumu, içerik kalitesi ve anahtar kelime hedeflemesini gözden geçirin.",
                DocUrl = "https://developers.google.com/search/docs/monitor-debug/search-console-start",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }

        if (scData.TotalImpressions28d > 100)
        {
            var ctr = scData.TotalClicks28d / (double)scData.TotalImpressions28d;
            if (ctr < 0.01)
            {
                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = run.NormalizedUrl,
                    RuleId = "RANK-002",
                    Category = "ranking",
                    Severity = AuditIssueSeverity.Warning,
                    Source = AuditIssueSource.SearchConsole,
                    Message = $"Organik CTR düşük (%{(ctr * 100):F1}) — snippet çekiciliği zayıf olabilir.",
                    FixHint = "Title ve meta description'ı iyileştirin; zengin sonuç uygunluğunu kontrol edin.",
                    DocUrl = "https://developers.google.com/search/docs/appearance/title-link",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);
            }

            foreach (var q in scData.TopQueries.Where(q => q.Impressions >= 50 && q.Ctr < 0.005))
            {
                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = run.NormalizedUrl,
                    RuleId = "RANK-003",
                    Category = "ranking",
                    Severity = AuditIssueSeverity.Info,
                    Source = AuditIssueSource.SearchConsole,
                    Message = $"Yüksek gösterim, düşük tıklama: \"{q.Query}\" ({q.Impressions} gösterim, CTR %{(q.Ctr * 100):F2}).",
                    FixHint = "Bu sorgu için title/description ve sayfa içeriğini güçlendirin.",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);
                break;
            }
        }

        foreach (var inspection in scData.UrlInspections)
        {
            if (inspection.Verdict is "PASS" or "NEUTRAL") continue;

            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = inspection.Url,
                RuleId = "INDEX-001",
                Category = "index-status",
                Severity = AuditIssueSeverity.Warning,
                Source = AuditIssueSource.SearchConsole,
                Message = $"Google URL Denetimi: {inspection.Verdict} — {inspection.CoverageState}",
                Evidence = inspection.IndexingState,
                FixHint = "İndeks engellerini kontrol edin: noindex, robots.txt, canonical veya kalite sorunları.",
                DocUrl = "https://developers.google.com/search/docs/crawling-indexing/robots-meta-tag",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);

            if (inspection.RichResultsVerdict is "FAIL" or "PARTIAL")
            {
                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = inspection.Url,
                    RuleId = "RICH-001",
                    Category = "structured-data",
                    Severity = AuditIssueSeverity.Warning,
                    Source = AuditIssueSource.SearchConsole,
                    Message = $"Zengin sonuç sorunu tespit edildi: {inspection.RichResultsVerdict}",
                    FixHint = "Search Console URL Denetimi'nde gösterilen yapılandırılmış veri hatalarını düzeltin.",
                    DocUrl = "https://developers.google.com/search/docs/appearance/structured-data",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);
            }
        }

        run.ProgressMessage = "Search Console kapsam ve site haritaları…";
        await _scCoverage.AnalyzeAsync(run, accessToken, propertyUrl, scData, cancellationToken);

        run.ProgressMessage = "Anahtar kelime trendi karşılaştırılıyor…";
        await _keywordTrend.CompareWithPreviousAuditAsync(run, cancellationToken);
    }

    private async Task CheckSafeBrowsingAsync(AuditRun run, CancellationToken cancellationToken)
    {
        var apiKey = _config["Google:SafeBrowsingApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        var client = _httpClientFactory.CreateClient();
        var payload = new
        {
            client = new { clientId = "search-console-app", clientVersion = "1.0" },
            threatInfo = new
            {
                threatTypes = new[] { "MALWARE", "SOCIAL_ENGINEERING", "UNWANTED_SOFTWARE" },
                platformTypes = new[] { "ANY_PLATFORM" },
                threatEntryTypes = new[] { "URL" },
                threatEntries = new[] { new { url = run.NormalizedUrl } },
            },
        };

        try
        {
            var response = await client.PostAsJsonAsync(
                $"https://safebrowsing.googleapis.com/v4/threatMatches:find?key={apiKey}",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("matches", out var matches) || matches.GetArrayLength() == 0)
                return;

            var threat = matches[0].GetProperty("threatType").GetString() ?? "UNKNOWN";
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = run.NormalizedUrl,
                RuleId = "SAFE-001",
                Category = "security",
                Severity = AuditIssueSeverity.Critical,
                Source = AuditIssueSource.SafeBrowsing,
                Message = $"Google Safe Browsing bu siteyi işaretledi: {threat}",
                FixHint = "Sitedeki kötü amaçlı yazılım, kimlik avı veya istenmeyen yazılımı araştırın.",
                DocUrl = "https://developers.google.com/search/docs/monitor-debug/security",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Safe Browsing check failed for {Url}", run.NormalizedUrl);
        }
    }

    private async Task CheckPageSpeedAsync(AuditRun run, CancellationToken cancellationToken)
    {
        var apiKey = _config["Google:PageSpeedApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        var maxPages = _config.GetValue("Audit:PageSpeedMaxPages", 20);
        var urls = await _pageRepository.Table
            .Where(p => p.AuditRunId == run.Id)
            .OrderBy(p => p.CrawlDepth)
            .Select(p => p.Url)
            .Take(maxPages)
            .ToListAsync(cancellationToken);

        if (urls.Count == 0) urls.Add(run.NormalizedUrl);

        var client = _httpClientFactory.CreateClient();
        foreach (var pageUrl in urls)
        {
            await RunPageSpeedForUrlAsync(run, client, apiKey, pageUrl, cancellationToken);
        }
    }

    private async Task RunPageSpeedForUrlAsync(
        AuditRun run, HttpClient client, string apiKey, string pageUrl, CancellationToken cancellationToken)
    {
        var url = Uri.EscapeDataString(pageUrl);
        var psiUrl =
            $"https://www.googleapis.com/pagespeedonline/v5/runPagespeed?url={url}&strategy=mobile&category=performance&key={apiKey}";

        try
        {
            var response = await client.GetAsync(psiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("lighthouseResult", out var lighthouse)) return;
            if (!lighthouse.TryGetProperty("categories", out var categories)) return;
            if (!categories.TryGetProperty("performance", out var perf)) return;

            var score = perf.TryGetProperty("score", out var scoreEl) ? (int)(scoreEl.GetDouble() * 100) : 100;
            var audits = lighthouse.GetProperty("audits");
            var lcp = ReadMetric(audits, "largest-contentful-paint");
            var inp = ReadMetric(audits, "interaction-to-next-paint");
            var cls = ReadMetric(audits, "cumulative-layout-shift");

            await _pageSpeedRepository.InsertAsync(new PageSpeedResult
            {
                AuditRunId = run.Id,
                Url = pageUrl,
                PerformanceScore = score,
                Lcp = lcp,
                Inp = inp,
                Cls = cls,
                Strategy = "mobile",
                CreatedAtUtc = DateTime.UtcNow,
            }, publishEvent: false);

            await AddCwvIssuesAsync(run, pageUrl, lcp, inp, cls, score, cancellationToken);
            await AddMobileFriendlyIssuesAsync(run, pageUrl, audits, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PageSpeed check failed for {Url}", pageUrl);
        }
    }

    private async Task AddCwvIssuesAsync(
        AuditRun run, string pageUrl, string lcp, string inp, string cls, int score,
        CancellationToken cancellationToken)
    {
        if (ParseSeconds(lcp) > 4.0)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id, PageUrl = pageUrl, RuleId = "CWV-001", Category = "core-web-vitals",
                Severity = AuditIssueSeverity.Warning, Source = AuditIssueSource.PageSpeed,
                Message = $"LCP kötü: {lcp}.", FixHint = "Ana içeriği hızlandırın; büyük görselleri optimize edin.",
                DocUrl = "https://developers.google.com/search/docs/appearance/core-web-vitals",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }

        if (ParseSeconds(inp) > 0.5)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id, PageUrl = pageUrl, RuleId = "CWV-002", Category = "core-web-vitals",
                Severity = AuditIssueSeverity.Warning, Source = AuditIssueSource.PageSpeed,
                Message = $"INP kötü: {inp}.", FixHint = "JavaScript yükünü azaltın; etkileşim gecikmesini düşürün.",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }

        if (ParseCls(cls) > 0.25)
        {
            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id, PageUrl = pageUrl, RuleId = "CWV-003", Category = "core-web-vitals",
                Severity = AuditIssueSeverity.Warning, Source = AuditIssueSource.PageSpeed,
                Message = $"CLS kötü: {cls}.", FixHint = "Görsel/video boyutlarını rezerve edin; layout kaymasını azaltın.",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }

        if (score >= 50) return;

        await _issueWriter.RecordAsync(run, new AuditIssue
        {
            AuditRunId = run.Id,
            PageUrl = pageUrl,
            RuleId = "CWV-004",
            Category = "core-web-vitals",
            Severity = score < 30 ? AuditIssueSeverity.Critical : AuditIssueSeverity.Warning,
            Source = AuditIssueSource.PageSpeed,
            Message = $"Mobil PageSpeed performans skoru {score}/100.",
            Evidence = IssueDetailEvidenceBuilder.Build(
                $"Mobil PageSpeed performans skoru {score}/100",
                [
                    new() { Label = "Sayfa", Value = pageUrl, Href = pageUrl.StartsWith("http") ? pageUrl : null },
                    new() { Label = "Skor", Value = $"{score}/100" },
                    new() { Label = "LCP", Value = lcp ?? "—" },
                    new() { Label = "INP", Value = inp ?? "—" },
                    new() { Label = "CLS", Value = cls ?? "—" },
                ]),
            FixHint = "Core Web Vitals'ı iyileştirin — LCP'yi optimize edin, düzen kaymasını azaltın, engelleyici kaynakları küçültün.",
            DocUrl = "https://developers.google.com/search/docs/appearance/core-web-vitals",
            CreatedAt = DateTime.UtcNow,
        }, cancellationToken);
    }

    private async Task AddMobileFriendlyIssuesAsync(
        AuditRun run, string pageUrl, JsonElement audits, CancellationToken cancellationToken)
    {
        await CheckLighthouseAuditAsync(run, pageUrl, audits, "viewport", "MOB-001",
            "Viewport meta etiketi eksik veya hatalı — mobil uyumluluk riski.",
            "width=device-width, initial-scale=1 viewport meta ekleyin.",
            AuditIssueSeverity.Critical, cancellationToken);

        await CheckLighthouseAuditAsync(run, pageUrl, audits, "tap-targets", "MOB-002",
            "Dokunma hedefleri çok küçük veya yakın — mobil kullanılabilirlik sorunu.",
            "Buton/link boyutlarını en az 48px yapın; hedefler arası boşluk bırakın.",
            AuditIssueSeverity.Warning, cancellationToken);

        await CheckLighthouseAuditAsync(run, pageUrl, audits, "font-size", "MOB-003",
            "Okunabilir font boyutu yetersiz — mobilde zoom gerektirebilir.",
            "Gövde metni için en az 16px font kullanın.",
            AuditIssueSeverity.Warning, cancellationToken);
    }

    private async Task CheckLighthouseAuditAsync(
        AuditRun run, string pageUrl, JsonElement audits, string auditKey, string ruleId,
        string message, string fixHint, AuditIssueSeverity severity, CancellationToken cancellationToken)
    {
        if (!audits.TryGetProperty(auditKey, out var audit)) return;
        var auditScore = audit.TryGetProperty("score", out var s) ? s.GetDouble() : 1;
        if (auditScore >= 0.9) return;

        await _issueWriter.RecordAsync(run, new AuditIssue
        {
            AuditRunId = run.Id,
            PageUrl = pageUrl,
            RuleId = ruleId,
            Category = "mobile-friendly",
            Severity = severity,
            Source = AuditIssueSource.PageSpeed,
            Message = message,
            Evidence = audit.TryGetProperty("displayValue", out var dv) ? dv.GetString() : null,
            FixHint = fixHint,
            DocUrl = "https://developers.google.com/search/docs/crawling-indexing/mobile/mobile-sites-mobile-first-indexing",
            CreatedAt = DateTime.UtcNow,
        }, cancellationToken);
    }

    private static double ParseSeconds(string display)
    {
        if (string.IsNullOrEmpty(display) || display == "n/a") return 0;
        var num = new string(display.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(num, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static double ParseCls(string display)
    {
        if (string.IsNullOrEmpty(display) || display == "n/a") return 0;
        var num = new string(display.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(num, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static string ReadMetric(JsonElement audits, string key)
    {
        if (!audits.TryGetProperty(key, out var audit)) return "n/a";
        if (audit.TryGetProperty("displayValue", out var display)) return display.GetString() ?? "n/a";
        return "n/a";
    }
}
