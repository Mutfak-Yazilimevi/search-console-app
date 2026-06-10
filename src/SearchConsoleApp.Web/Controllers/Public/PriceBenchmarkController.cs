using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SearchConsoleApp.Services.PriceBenchmark;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.RateLimiting;

namespace SearchConsoleApp.Web.Controllers.Public;

public record StartPriceBenchmarkRequest(string Url);

public record PriceBenchmarkWebhookRequest(
    Guid PriceBenchmarkRunEntityId,
    string? Event,
    string? Url,
    string? Title,
    decimal? OurPrice,
    string? PriceCurrency,
    string? ExtractedProductJson,
    string? ShoppingOffersJson,
    decimal? MinMarketPrice,
    decimal? MaxMarketPrice,
    decimal? WeightedAvgMarketPrice,
    string? MinOfferLink,
    string? MinOfferSource,
    string? MaxOfferLink,
    string? MaxOfferSource,
    int? MarketOfferCount,
    decimal? DeltaPercent,
    string? MarketPosition,
    string? ShoppingError,
    int? TotalProducts,
    bool? SerpApiConfigured,
    string? ErrorMessage,
    string? ProgressPhase,
    string? ProgressMessage);

[Route("api/v{version:apiVersion}/public/price-benchmark")]
public class PriceBenchmarkController : PublicApiController
{
    private readonly IPriceBenchmarkService _service;
    private readonly IConfiguration _config;

    public PriceBenchmarkController(IPriceBenchmarkService service, IConfiguration config)
    {
        _service = service;
        _config = config;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitingSetup.AuditPolicy)]
    public async Task<IActionResult> Start([FromBody] StartPriceBenchmarkRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return ValidationProblem(new Dictionary<string, string[]> { ["url"] = ["URL is required."] });

        try
        {
            var run = await _service.StartAsync(request.Url, cancellationToken);
            return Ok(PriceBenchmarkMapper.MapRun(run));
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

    [HttpPost("{entityId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid entityId, CancellationToken cancellationToken)
    {
        await _service.CancelAsync(entityId, cancellationToken);
        var detail = await _service.GetDetailAsync(entityId, cancellationToken);
        if (detail == null) return NotFound();
        return Ok(detail.Run);
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] PriceBenchmarkWebhookRequest request, CancellationToken cancellationToken)
    {
        var secret = _config["Audit:WebhookSecret"];
        if (!string.IsNullOrWhiteSpace(secret))
        {
            if (!Request.Headers.TryGetValue("X-Audit-Webhook-Secret", out var provided) || provided != secret)
                return Unauthorized(new { success = false, message = "Invalid webhook secret." });
        }

        if (request.PriceBenchmarkRunEntityId == Guid.Empty)
            return BadRequest(new { error = "priceBenchmarkRunEntityId is required." });

        switch (request.Event?.ToLowerInvariant())
        {
            case "discovered":
                await _service.ProcessDiscoveredProductAsync(request.PriceBenchmarkRunEntityId, new PriceBenchmarkProductPayload(
                    request.Url,
                    request.Title,
                    request.OurPrice,
                    request.PriceCurrency,
                    request.ExtractedProductJson,
                    null,
                    null, null, null,
                    null, null, null, null,
                    null, null, null,
                    request.ShoppingError,
                    request.ProgressPhase,
                    request.ProgressMessage), cancellationToken);
                break;
            case "discover-complete":
                await _service.ProcessDiscoverCompleteAsync(request.PriceBenchmarkRunEntityId, new PriceBenchmarkProductPayload(
                    null, null, null, null, null, null,
                    null, null, null, null, null, null, null, null,
                    null, null, null,
                    request.ProgressPhase,
                    request.ProgressMessage), cancellationToken);
                break;
            case "compared":
            case "product":
                await _service.ProcessComparedProductAsync(request.PriceBenchmarkRunEntityId, new PriceBenchmarkProductPayload(
                    request.Url,
                    request.Title,
                    request.OurPrice,
                    request.PriceCurrency,
                    request.ExtractedProductJson,
                    request.ShoppingOffersJson,
                    request.MinMarketPrice,
                    request.MaxMarketPrice,
                    request.WeightedAvgMarketPrice,
                    request.MinOfferLink,
                    request.MinOfferSource,
                    request.MaxOfferLink,
                    request.MaxOfferSource,
                    request.MarketOfferCount,
                    request.DeltaPercent,
                    request.MarketPosition,
                    request.ShoppingError,
                    request.ProgressPhase,
                    request.ProgressMessage), cancellationToken);
                break;
            case "complete":
                await _service.CompleteAsync(request.PriceBenchmarkRunEntityId, new PriceBenchmarkCompletePayload(
                    request.TotalProducts ?? 0,
                    request.SerpApiConfigured ?? false), cancellationToken);
                break;
            case "failed":
                await _service.FailAsync(
                    request.PriceBenchmarkRunEntityId,
                    request.ErrorMessage ?? "Crawl failed",
                    cancellationToken);
                break;
            default:
                return BadRequest(new { error = "Unknown event." });
        }

        return Ok(new { success = true });
    }
}
