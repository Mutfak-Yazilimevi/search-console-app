namespace SearchConsoleApp.Core.Domain.PriceBenchmark;

public partial class PriceBenchmarkItem : BaseEntity
{
    public long RunId { get; set; }
    public string PageUrl { get; set; } = "";
    public string? Title { get; set; }
    public decimal? OurPrice { get; set; }
    public string? PriceCurrency { get; set; }
    public decimal? MinMarketPrice { get; set; }
    public decimal? MaxMarketPrice { get; set; }
    public decimal? WeightedAvgMarketPrice { get; set; }
    public string? MinOfferLink { get; set; }
    public string? MinOfferSource { get; set; }
    public string? MaxOfferLink { get; set; }
    public string? MaxOfferSource { get; set; }
    public int MarketOfferCount { get; set; }
    public decimal? DeltaPercent { get; set; }
    public MarketPricePosition MarketPosition { get; set; } = MarketPricePosition.Unknown;
    public string? ShoppingError { get; set; }
    public string ExtractedDataJson { get; set; } = "{}";
    public string ShoppingOffersJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
}
