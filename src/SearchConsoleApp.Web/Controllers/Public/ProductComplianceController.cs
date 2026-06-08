using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SearchConsoleApp.Services.Audit;
using SearchConsoleApp.Services.Auth;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Services.MerchantCenter;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.RateLimiting;
using System.Security.Claims;

namespace SearchConsoleApp.Web.Controllers.Public;

public record StartProductComplianceRequest(string Url);

public record ProductComplianceWebhookRequest(
    Guid ProductComplianceRunEntityId,
    Guid? ProductItemEntityId,
    string? Event,
    string? Url,
    string? Title,
    string? ExtractedProductJson,
    int? TotalProducts,
    string? SiteCheckHtml,
    string? ErrorMessage,
    string? ProgressPhase,
    string? ProgressMessage);

public record GmcAiGenerateRequest(string Type);

[Route("api/v{version:apiVersion}/public/merchant-center/compliance")]
public class ProductComplianceController : PublicApiController
{
    private readonly IProductComplianceService _service;
    private readonly IGeminiGmcComplianceService _aiService;
    private readonly IGmcIntegrationStatusService _integrationStatusService;
    private readonly IConfiguration _config;

    public ProductComplianceController(
        IProductComplianceService service,
        IGeminiGmcComplianceService aiService,
        IGmcIntegrationStatusService integrationStatusService,
        IConfiguration config)
    {
        _service = service;
        _aiService = aiService;
        _integrationStatusService = integrationStatusService;
        _config = config;
    }

    [HttpGet("integrations/status")]
    public IActionResult GetIntegrationStatus()
        => Ok(_integrationStatusService.GetStatus());

    [HttpPost]
    [EnableRateLimiting(RateLimitingSetup.AuditPolicy)]
    public async Task<IActionResult> Start([FromBody] StartProductComplianceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return ValidationProblem(new Dictionary<string, string[]> { ["url"] = ["URL is required."] });

        try
        {
            var run = await _service.StartAsync(request.Url, null, null, cancellationToken);
            return Ok(MapRun(run));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(new Dictionary<string, string[]> { ["url"] = [ex.Message] });
        }
    }

    [HttpGet("{entityId:guid}")]
    public async Task<IActionResult> Get(Guid entityId, CancellationToken cancellationToken)
    {
        var detail = await _service.GetDetailAsync(entityId, cancellationToken);
        if (detail == null) return NotFound();
        return Ok(detail);
    }

    [HttpGet("{entityId:guid}/products")]
    public async Task<IActionResult> GetProducts(
        Guid entityId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var products = await _service.GetProductsAsync(entityId, skip, take, cancellationToken);
        return Ok(products);
    }

    [HttpGet("{entityId:guid}/products/{productId:guid}")]
    public async Task<IActionResult> GetProduct(Guid entityId, Guid productId, CancellationToken cancellationToken)
    {
        var detail = await _service.GetProductDetailAsync(entityId, productId, cancellationToken);
        if (detail == null) return NotFound();
        return Ok(detail);
    }

    [HttpGet("{entityId:guid}/export")]
    public async Task<IActionResult> Export(
        Guid entityId,
        [FromQuery] string? format,
        CancellationToken cancellationToken)
    {
        if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
        {
            var html = await _service.ExportHtmlReportAsync(entityId, cancellationToken);
            if (html == null) return NotFound();
            return Content(html, "text/html; charset=utf-8");
        }

        var detail = await _service.GetExportDetailAsync(entityId, cancellationToken);
        if (detail == null) return NotFound();
        return Ok(detail);
    }

    [HttpPost("{entityId:guid}/ai/summary")]
    public async Task<IActionResult> AiSummary(Guid entityId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _aiService.GenerateActionSummaryAsync(entityId, cancellationToken);
            return Ok(result);
        }
        catch (AiGenerationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    [HttpPost("{entityId:guid}/products/{productId:guid}/ai/generate")]
    public async Task<IActionResult> AiGenerateProduct(
        Guid entityId,
        Guid productId,
        [FromBody] GmcAiGenerateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _aiService.GenerateForProductAsync(entityId, productId, request.Type, cancellationToken);
            return Ok(result);
        }
        catch (AiGenerationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    [HttpPost("{entityId:guid}/ai/site/generate")]
    public async Task<IActionResult> AiGenerateSite(
        Guid entityId,
        [FromBody] GmcAiGenerateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _aiService.GenerateSiteContentAsync(entityId, request.Type, cancellationToken);
            return Ok(result);
        }
        catch (AiGenerationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    [HttpPost("{entityId:guid}/ai/issues/{issueId:guid}/explain")]
    public async Task<IActionResult> AiExplainIssue(
        Guid entityId,
        Guid issueId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _aiService.ExplainIssueAsync(entityId, issueId, cancellationToken);
            return Ok(result);
        }
        catch (AiGenerationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    [HttpPost("{entityId:guid}/products/{productId:guid}/rescan")]
    public async Task<IActionResult> RescanProduct(
        Guid entityId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.RescanProductAsync(entityId, productId, cancellationToken);
            return Ok(new { ok = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{entityId:guid}/ai/bulk-generate")]
    public async Task<IActionResult> AiBulkGenerate(
        Guid entityId,
        [FromBody] GmcAiGenerateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _aiService.BulkGenerateForTopProductsAsync(
                entityId, request.Type, maxProducts: 5, cancellationToken);
            return Ok(results);
        }
        catch (AiGenerationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }

    [HttpPost("{entityId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid entityId, CancellationToken cancellationToken)
    {
        await _service.CancelAsync(entityId, cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] ProductComplianceWebhookRequest request, CancellationToken cancellationToken)
    {
        var secret = _config["Audit:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(secret))
        {
            if (!Request.Headers.TryGetValue("X-Audit-Webhook-Secret", out var provided) || provided != secret)
                return Unauthorized(new { success = false, message = "Invalid webhook secret." });
        }

        if (request.ProductComplianceRunEntityId == Guid.Empty)
            return BadRequest(new { error = "productComplianceRunEntityId is required." });

        switch (request.Event?.ToLowerInvariant())
        {
            case "product":
                await _service.ProcessProductAsync(
                    request.ProductComplianceRunEntityId,
                    new ProductComplianceCrawlProductPayload(
                        request.Url ?? "",
                        request.Title,
                        request.ExtractedProductJson ?? "{}"),
                    cancellationToken);
                break;
            case "complete":
                await _service.CompleteCrawlAsync(
                    request.ProductComplianceRunEntityId,
                    new ProductComplianceCrawlCompletePayload(
                        request.TotalProducts ?? 0,
                        request.SiteCheckHtml),
                    cancellationToken);
                break;
            case "product-rescan":
                if (!request.ProductItemEntityId.HasValue || request.ProductItemEntityId == Guid.Empty)
                    return BadRequest(new { error = "productItemEntityId is required for product-rescan." });
                await _service.ProcessProductRescanAsync(
                    request.ProductComplianceRunEntityId,
                    request.ProductItemEntityId.Value,
                    new ProductComplianceCrawlProductPayload(
                        request.Url ?? "",
                        request.Title,
                        request.ExtractedProductJson ?? "{}"),
                    cancellationToken);
                break;
            case "rescan-complete":
                if (!request.ProductItemEntityId.HasValue || request.ProductItemEntityId == Guid.Empty)
                    return BadRequest(new { error = "productItemEntityId is required for rescan-complete." });
                await _service.CompleteProductRescanAsync(
                    request.ProductComplianceRunEntityId,
                    request.ProductItemEntityId.Value,
                    cancellationToken);
                break;
            case "rescan-failed":
                await _service.FailProductRescanAsync(
                    request.ProductComplianceRunEntityId,
                    request.ErrorMessage ?? "Ürün yeniden tarama başarısız.",
                    cancellationToken);
                break;
            case "failed":
                await _service.FailCrawlAsync(
                    request.ProductComplianceRunEntityId,
                    request.ErrorMessage ?? "Tarama başarısız.",
                    cancellationToken);
                break;
            default:
                return BadRequest(new { error = "event must be 'product', 'complete', 'product-rescan', 'rescan-complete', 'rescan-failed', or 'failed'." });
        }

        return Ok(new { ok = true });
    }

    private static object MapRun(Core.Domain.MerchantCenter.ProductComplianceRun run) => new
    {
        run.EntityId,
        run.InputUrl,
        run.NormalizedUrl,
        Status = run.Status.ToString(),
        AnalysisMode = run.AnalysisMode.ToString(),
        run.CreatedAt,
        run.StartedAt,
        run.CompletedAt,
        run.TotalProducts,
        run.CompliantCount,
        run.PartialCount,
        run.NonCompliantCount,
        run.ComplianceScore,
        run.SiteReadinessScore,
        run.CriticalCount,
        run.WarningCount,
        run.InfoCount,
        run.ErrorMessage,
        run.ProgressPhase,
        run.ProgressMessage,
        run.MerchantCenterAccountId,
    };
}
