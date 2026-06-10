using SearchConsoleApp.Core.Domain.PriceBenchmark;

namespace SearchConsoleApp.Services.PriceBenchmark;

public record PriceBenchmarkRunDto(
    Guid EntityId,
    string InputUrl,
    string NormalizedUrl,
    string Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int TotalProducts,
    bool SerpApiConfigured,
    string? ErrorMessage,
    string? ProgressPhase,
    string? ProgressMessage);

public record PriceBenchmarkItemDto(
    Guid EntityId,
    string PageUrl,
    string? Title,
    decimal? OurPrice,
    string? PriceCurrency,
    decimal? MinMarketPrice,
    decimal? MaxMarketPrice,
    decimal? WeightedAvgMarketPrice,
    string? MinOfferLink,
    string? MinOfferSource,
    string? MaxOfferLink,
    string? MaxOfferSource,
    int MarketOfferCount,
    decimal? DeltaPercent,
    string MarketPosition,
    string? ShoppingError,
    string ShoppingOffersJson);

public record PriceBenchmarkDetailDto(
    PriceBenchmarkRunDto Run,
    IReadOnlyList<PriceBenchmarkItemDto> Products);

public record PriceBenchmarkProductPayload(
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
    string? ProgressPhase,
    string? ProgressMessage);

public record PriceBenchmarkCompletePayload(
    int TotalProducts,
    bool SerpApiConfigured);

public static class PriceBenchmarkMapper
{
    public static PriceBenchmarkRunDto MapRun(PriceBenchmarkRun run) => new(
        run.EntityId,
        run.InputUrl,
        run.NormalizedUrl,
        run.Status.ToString(),
        run.CreatedAt,
        run.StartedAt,
        run.CompletedAt,
        run.TotalProducts,
        run.SerpApiConfigured,
        run.ErrorMessage,
        run.ProgressPhase,
        run.ProgressMessage);

    public static PriceBenchmarkItemDto MapItem(PriceBenchmarkItem item) => new(
        item.EntityId,
        item.PageUrl,
        item.Title,
        item.OurPrice,
        item.PriceCurrency,
        item.MinMarketPrice,
        item.MaxMarketPrice,
        item.WeightedAvgMarketPrice,
        item.MinOfferLink,
        item.MinOfferSource,
        item.MaxOfferLink,
        item.MaxOfferSource,
        item.MarketOfferCount,
        item.DeltaPercent,
        item.MarketPosition.ToString().ToLowerInvariant(),
        item.ShoppingError,
        item.ShoppingOffersJson);

    public static MarketPricePosition ParsePosition(string? value) =>
        value?.ToLowerInvariant() switch
        {
            "below" => MarketPricePosition.Below,
            "above" => MarketPricePosition.Above,
            "average" => MarketPricePosition.Average,
            _ => MarketPricePosition.Unknown,
        };
}
