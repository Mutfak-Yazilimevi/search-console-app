using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Services.Audit;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.RateLimiting;

namespace SearchConsoleApp.Web.Controllers.Public;

public record StartAuditRequest(string Url);

public record AuditRunResponse(
    Guid EntityId,
    string InputUrl,
    string NormalizedUrl,
    string Status,
    string Mode,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int PagesCrawled,
    int IssuesFound,
    int CriticalCount,
    int WarningCount,
    int InfoCount,
    int? Score,
    string? ErrorMessage,
    string? ProgressPhase,
    string? ProgressMessage,
    string? SearchConsolePropertyUrl);

public record PerformanceResponse(
    string PropertyUrl,
    int TotalClicks28d,
    int TotalImpressions28d,
    IList<PerformanceQueryRow> TopQueries);

public record PerformanceQueryRow(string? Query, int Clicks, int Impressions, double Ctr, double Position);

public record ContentQualityResponse(
    string Url,
    int EeatScore,
    string ChecklistJson,
    string? SuggestionsJson);

public record ScannedPageResponse(
    Guid EntityId,
    string Url,
    int? StatusCode,
    string? Title,
    string? MetaDescription,
    int CrawlDepth,
    int? ResponseTimeMs,
    DateTime ScannedAt);

public record AuditIssueResponse(
    Guid EntityId,
    string PageUrl,
    string RuleId,
    string Category,
    string Severity,
    string Source,
    string Message,
    string? Evidence,
    string? FixHint,
    string? DocUrl,
    DateTime CreatedAt);

public record AuditDetailResponse(
    AuditRunResponse Run,
    IList<ScannedPageResponse> Pages,
    IList<AuditIssueResponse> Issues);

/// <summary>
/// Public SEO audit endpoints.
/// Route: /api/v1/public/audit/*
/// </summary>
public class AuditController : PublicApiController
{
    private readonly IAuditService _auditService;
    private readonly IGeminiFaqGenerationService _geminiFaqGenerationService;
    private readonly IAuditIntegrationStatusService _integrationStatusService;
    private readonly IIntegrationSettingsService _integrationSettingsService;
    private readonly IConfiguration _config;

    public AuditController(
        IAuditService auditService,
        IGeminiFaqGenerationService geminiFaqGenerationService,
        IAuditIntegrationStatusService integrationStatusService,
        IIntegrationSettingsService integrationSettingsService,
        IConfiguration config)
    {
        _auditService = auditService;
        _geminiFaqGenerationService = geminiFaqGenerationService;
        _integrationStatusService = integrationStatusService;
        _integrationSettingsService = integrationSettingsService;
        _config = config;
    }

    [HttpGet("integrations/status")]
    public IActionResult GetIntegrationStatus()
    {
        return Ok(_integrationStatusService.GetGlobalStatus());
    }

    [HttpPatch("integrations/{integrationId}")]
    public async Task<IActionResult> UpdateIntegration(
        string integrationId,
        [FromBody] UpdateIntegrationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _integrationSettingsService.UpdateAsync(integrationId, request, cancellationToken);
            return Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{entityId:guid}/integrations")]
    public async Task<IActionResult> GetRunIntegrations(Guid entityId, CancellationToken cancellationToken)
    {
        var status = await _integrationStatusService.GetRunStatusAsync(entityId, cancellationToken);
        if (status == null) return NotFoundResult("Audit run not found.");
        return Ok(status);
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitingSetup.AuditPolicy)]
    public async Task<IActionResult> Start([FromBody] StartAuditRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return ValidationProblem(new Dictionary<string, string[]>
            {
                ["url"] = ["URL is required."],
            });

        try
        {
            var run = await _auditService.StartAuditAsync(request.Url, cancellationToken);
            return Ok(MapRun(run));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(new Dictionary<string, string[]>
            {
                ["url"] = [ex.Message],
            });
        }
        catch (AuditQuotaException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{entityId:guid}")]
    public async Task<IActionResult> Get(Guid entityId, CancellationToken cancellationToken)
    {
        var detail = await _auditService.GetAuditAsync(entityId, cancellationToken);
        if (detail == null) return NotFoundResult("Audit run not found.");

        return Ok(new AuditDetailResponse(
            MapRun(detail.Run),
            detail.Pages.Select(p => new ScannedPageResponse(
                p.EntityId, p.Url, p.StatusCode, p.Title, p.MetaDescription, p.CrawlDepth, p.ResponseTimeMs, p.ScannedAt)).ToList(),
            detail.Issues.Select(i => new AuditIssueResponse(
                i.EntityId, i.PageUrl ?? "", i.RuleId, i.Category, i.Severity.ToString(), i.Source.ToString(),
                i.Message, i.Evidence, i.FixHint, i.DocUrl, i.CreatedAt)).ToList()));
    }

    [HttpGet("{entityId:guid}/performance")]
    public async Task<IActionResult> GetPerformance(Guid entityId, CancellationToken cancellationToken)
    {
        var perf = await _auditService.GetPerformanceAsync(entityId, cancellationToken);
        if (perf == null)
            return Ok(new { available = false });
        return Ok(new PerformanceResponse(
            perf.PropertyUrl,
            perf.TotalClicks28d,
            perf.TotalImpressions28d,
            perf.TopQueries.Select(q => new PerformanceQueryRow(q.Query, q.Clicks, q.Impressions, q.Ctr, q.Position)).ToList()));
    }

    [HttpGet("{entityId:guid}/content-quality")]
    public async Task<IActionResult> GetContentQuality(Guid entityId, CancellationToken cancellationToken)
    {
        var scores = await _auditService.GetContentQualityAsync(entityId, cancellationToken);
        return Ok(scores.Select(s => new ContentQualityResponse(s.Url, s.EeatScore, s.ChecklistJson, s.SuggestionsJson)).ToList());
    }

    [HttpGet("{entityId:guid}/pagespeed")]
    public async Task<IActionResult> GetPageSpeed(Guid entityId, CancellationToken cancellationToken)
    {
        var rows = await _auditService.GetPageSpeedAsync(entityId, cancellationToken);
        if (rows.Count == 0) return Ok(new { available = false });
        return Ok(rows.Select(p => new
        {
            p.Url,
            p.PerformanceScore,
            p.Lcp,
            p.Inp,
            p.Cls,
            p.Strategy,
        }));
    }

    [HttpGet("{entityId:guid}/index-status")]
    public async Task<IActionResult> GetIndexStatus(Guid entityId, CancellationToken cancellationToken)
    {
        var snap = await _auditService.GetIndexStatusAsync(entityId, cancellationToken);
        if (snap == null) return Ok(new { available = false });
        return Ok(new
        {
            available = true,
            snap.Domain,
            snap.EstimatedIndexedPages,
            snap.CrawledPages,
            snap.CoverageRatio,
            snap.Source,
        });
    }

    [HttpGet("{entityId:guid}/keywords")]
    public async Task<IActionResult> GetKeywords(Guid entityId, CancellationToken cancellationToken)
    {
        var rows = await _auditService.GetTrackedKeywordsAsync(entityId, cancellationToken);
        if (rows.Count == 0) return Ok(new { available = false });
        return Ok(rows.Select(k => new
        {
            k.Keyword,
            k.Position,
            k.Impressions,
            k.Clicks,
            k.Ctr,
        }));
    }

    [HttpGet("{entityId:guid}/keyword-serp")]
    public async Task<IActionResult> GetKeywordSerp(Guid entityId, CancellationToken cancellationToken)
    {
        var rows = await _auditService.GetKeywordSerpAsync(entityId, cancellationToken);
        if (rows.Count == 0) return Ok(new { available = false });
        return Ok(rows.Select(s => new
        {
            s.Keyword,
            s.Position,
            s.MatchedUrl,
        }));
    }

    [HttpGet("{entityId:guid}/search-console-coverage")]
    public async Task<IActionResult> GetSearchConsoleCoverage(Guid entityId, CancellationToken cancellationToken)
    {
        var coverage = await _auditService.GetSearchConsoleCoverageAsync(entityId, cancellationToken);
        if (coverage == null) return Ok(new { available = false });
        return Ok(new
        {
            available = true,
            coverage.PropertyUrl,
            coverage.IndexedPages,
            coverage.ExcludedPages,
            coverage.InspectedCount,
            coverage.PassedCount,
            coverage.FailedCount,
            sitemaps = coverage.Sitemaps.Select(s => new
            {
                s.Path,
                s.Errors,
                s.Warnings,
                s.IsPending,
            }),
        });
    }

    [HttpGet("{entityId:guid}/backlinks")]
    public async Task<IActionResult> GetBacklinks(Guid entityId, CancellationToken cancellationToken)
    {
        var summary = await _auditService.GetBacklinksAsync(entityId, cancellationToken);
        if (summary == null) return Ok(new { available = false });
        return Ok(new
        {
            available = true,
            summary.InternalLinkCount,
            summary.UniqueInternalTargets,
            summary.OrphanPageCount,
            summary.ExternalReferringDomainCount,
            summary.ExternalBacklinkCount,
            summary.ExternalSource,
            externalTopDomains = ParseTopDomains(summary.ExternalTopDomainsJson),
        });
    }

    [HttpPost("{entityId:guid}/generate-faq")]
    [EnableRateLimiting(RateLimitingSetup.PublicPolicy)]
    public async Task<IActionResult> GenerateFaq(
        Guid entityId,
        [FromBody] GenerateFaqRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PageUrl))
            return ValidationProblem(new Dictionary<string, string[]> { ["pageUrl"] = ["pageUrl is required."] });

        try
        {
            var result = await _geminiFaqGenerationService.GenerateForPageAsync(
                entityId, request.PageUrl.Trim(), cancellationToken);

            return Ok(new GenerateFaqResponse(
                result.PageUrl,
                result.Questions.Select(q => new FaqItemResponse(q.Question, q.Answer)).ToList(),
                result.HtmlSection,
                result.JsonLd));
        }
        catch (AiGenerationException ex)
        {
            return GeminiUnavailable(ex);
        }
    }

    [HttpPost("{entityId:guid}/generate-meta")]
    [EnableRateLimiting(RateLimitingSetup.PublicPolicy)]
    public async Task<IActionResult> GenerateMeta(
        Guid entityId,
        [FromBody] GenerateMetaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PageUrl))
            return ValidationProblem(new Dictionary<string, string[]> { ["pageUrl"] = ["pageUrl is required."] });

        try
        {
            var target = string.IsNullOrWhiteSpace(request.Target) ? "seo" : request.Target.Trim();
            var result = await _geminiFaqGenerationService.GenerateMetaAsync(
                entityId, request.PageUrl.Trim(), target, cancellationToken);

            return Ok(new GenerateMetaResponse(
                result.PageUrl,
                result.Title,
                result.MetaDescription,
                result.TitleTagHtml,
                result.MetaTagHtml));
        }
        catch (AiGenerationException ex)
        {
            return GeminiUnavailable(ex);
        }
    }

    [HttpPost("{entityId:guid}/generate-alt-text")]
    [EnableRateLimiting(RateLimitingSetup.PublicPolicy)]
    public async Task<IActionResult> GenerateAltText(
        Guid entityId,
        [FromBody] GenerateAltTextRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PageUrl))
            return ValidationProblem(new Dictionary<string, string[]> { ["pageUrl"] = ["pageUrl is required."] });

        try
        {
            var result = await _geminiFaqGenerationService.GenerateAltTextAsync(
                entityId,
                request.PageUrl.Trim(),
                request.ImageSrcs?.ToList(),
                cancellationToken);

            return Ok(new GenerateAltTextResponse(
                result.PageUrl,
                result.Images.Select(i => new AltTextSuggestionResponse(i.Src, i.AltText, i.ImgHtmlSnippet)).ToList()));
        }
        catch (AiGenerationException ex)
        {
            return GeminiUnavailable(ex);
        }
    }

    private static IActionResult GeminiUnavailable(AiGenerationException ex) =>
        new ObjectResult(new
        {
            success = false,
            code = "gemini_config_missing",
            message = ex.Message,
            setupHint = "Google Cloud Console → API → Gemini API etkinleştirin. .env dosyasına GOOGLE_GEMINI_API_KEY ekleyin.",
            configKey = "Google:GeminiApiKey",
        })
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable,
        };

    private static IList<string> ParseTopDomains(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    [HttpGet("{entityId:guid}/export")]
    public async Task<IActionResult> Export(Guid entityId, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
        {
            var html = await _auditService.ExportHtmlReportAsync(entityId, cancellationToken);
            if (html == null) return NotFoundResult("Audit run not found.");
            return Content(html, "text/html; charset=utf-8");
        }

        if (string.Equals(format, "critical", StringComparison.OrdinalIgnoreCase))
        {
            var html = await _auditService.ExportCriticalHtmlReportAsync(entityId, cancellationToken);
            if (html == null) return NotFoundResult("Audit run not found.");
            return Content(html, "text/html; charset=utf-8");
        }

        var data = await _auditService.ExportAuditAsync(entityId, cancellationToken);
        if (data == null) return NotFoundResult("Audit run not found.");
        return Ok(data);
    }

    [HttpPost("{entityId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid entityId, CancellationToken cancellationToken)
    {
        var run = await _auditService.CancelAuditAsync(entityId, cancellationToken);
        if (run == null) return NotFoundResult("Audit run not found.");
        return Ok(MapRun(run));
    }

    [HttpGet("{entityId:guid}/events")]
    public async Task StreamEvents(Guid entityId, CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        while (!cancellationToken.IsCancellationRequested)
        {
            var detail = await _auditService.GetAuditAsync(entityId, cancellationToken);
            if (detail == null)
            {
                await Response.WriteAsync("event: error\ndata: not found\n\n", cancellationToken);
                break;
            }

            var run = detail.Run;
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = run.Status.ToString(),
                phase = run.ProgressPhase,
                message = run.ProgressMessage,
                pagesCrawled = run.PagesCrawled,
                issuesFound = run.IssuesFound,
                score = run.Score,
            });
            await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            if (run.Status is Core.Domain.Audit.AuditRunStatus.Completed
                or Core.Domain.Audit.AuditRunStatus.Failed
                or Core.Domain.Audit.AuditRunStatus.Cancelled)
                break;

            await Task.Delay(1500, cancellationToken);
        }
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] CrawlWebhookRequest request, CancellationToken cancellationToken)
    {
        var secret = _config["Audit:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(secret))
        {
            if (!Request.Headers.TryGetValue("X-Audit-Webhook-Secret", out var provided)
                || provided != secret)
            {
                return Unauthorized(new { success = false, message = "Invalid webhook secret." });
            }
        }

        if (request.AuditRunEntityId == Guid.Empty)
            return BadRequest(new { error = "auditRunEntityId is required." });

        switch (request.Event?.ToLowerInvariant())
        {
            case "page":
                await _auditService.ProcessCrawlPageAsync(
                    request.AuditRunEntityId,
                    new CrawlPagePayload(
                        request.Url ?? "",
                        request.StatusCode,
                        request.Title,
                        request.MetaDescription,
                        request.CrawlDepth,
                        request.ResponseTimeMs,
                        request.Issues?.Select(i => new CrawlIssuePayload(
                            i.RuleId, i.Category, i.Severity, i.Message, i.Evidence, i.FixHint, i.DocUrl)).ToList()
                            ?? [],
                        request.ProgressPhase,
                        request.ProgressMessage),
                    cancellationToken);
                break;

            case "complete":
                await _auditService.CompleteCrawlAsync(
                    request.AuditRunEntityId,
                    new CrawlCompletePayload(
                        request.TotalPages ?? 0,
                        request.InternalLinkCount ?? 0,
                        request.TopLinkedPagesJson),
                    cancellationToken);
                break;

            case "failed":
                await _auditService.FailCrawlAsync(
                    request.AuditRunEntityId,
                    request.ErrorMessage ?? "Tarama başarısız oldu.",
                    cancellationToken);
                break;

            default:
                return BadRequest(new { error = "event must be 'page', 'complete', or 'failed'." });
        }

        return Ok(new { ok = true });
    }

    private static AuditRunResponse MapRun(Core.Domain.Audit.AuditRun run) => new(
        run.EntityId,
        run.InputUrl,
        run.NormalizedUrl,
        run.Status.ToString(),
        run.Mode.ToString(),
        run.CreatedAt,
        run.StartedAt,
        run.CompletedAt,
        run.PagesCrawled,
        run.IssuesFound,
        run.CriticalCount,
        run.WarningCount,
        run.InfoCount,
        run.Score,
        run.ErrorMessage,
        run.ProgressPhase,
        run.ProgressMessage,
        run.SearchConsolePropertyUrl);
}

public record CrawlWebhookRequest(
    Guid AuditRunEntityId,
    string? Event,
    string? Url,
    int? StatusCode,
    string? Title,
    string? MetaDescription,
    int CrawlDepth,
    int? ResponseTimeMs,
    int? TotalPages,
    int? InternalLinkCount,
    string? TopLinkedPagesJson,
    string? ErrorMessage,
    string? ProgressPhase,
    string? ProgressMessage,
    IList<CrawlWebhookIssue>? Issues);

public record CrawlWebhookIssue(
    string RuleId,
    string Category,
    Core.Domain.Audit.AuditIssueSeverity Severity,
    string Message,
    string? Evidence,
    string? FixHint,
    string? DocUrl);

public record GenerateMetaRequest(string PageUrl, string? Target);

public record GenerateFaqRequest(string PageUrl);

public record FaqItemResponse(string Question, string Answer);

public record GenerateFaqResponse(
    string PageUrl,
    IList<FaqItemResponse> Questions,
    string HtmlSection,
    string JsonLd);

public record GenerateMetaResponse(
    string PageUrl,
    string Title,
    string MetaDescription,
    string TitleTagHtml,
    string MetaTagHtml);

public record GenerateAltTextRequest(string PageUrl, IList<string>? ImageSrcs);

public record AltTextSuggestionResponse(string Src, string AltText, string ImgHtmlSnippet);

public record GenerateAltTextResponse(
    string PageUrl,
    IList<AltTextSuggestionResponse> Images);
