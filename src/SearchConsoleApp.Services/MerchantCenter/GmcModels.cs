using System.Text.Json;
using System.Text.Json.Serialization;
using SearchConsoleApp.Core.Domain.MerchantCenter;

namespace SearchConsoleApp.Services.MerchantCenter;

public sealed class ExtractedProductData
{
    public string Url { get; set; } = "";
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Sku { get; set; }
    public string? VisibleSku { get; set; }
    public string? ProductId { get; set; }
    public string? Gtin { get; set; }
    public string? Mpn { get; set; }
    public string? Brand { get; set; }
    public string? Condition { get; set; }
    public string? Image { get; set; }
    public IList<string> Images { get; set; } = [];
    public int ImageCount { get; set; }
    public decimal? SchemaPrice { get; set; }
    public decimal? SchemaListPrice { get; set; }
    public decimal? VisiblePrice { get; set; }
    public string? PriceCurrency { get; set; }
    public string? Availability { get; set; }
    public bool? IdentifierExists { get; set; }
    public string? ItemGroupId { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    public bool HasProductSchema { get; set; }
    public bool HasMicrodataProduct { get; set; }
    public bool HasOgProductMeta { get; set; }
    public bool IsNoindex { get; set; }
    public string? CanonicalUrl { get; set; }
    public bool CanonicalPointsElsewhere { get; set; }
    public bool MainImageAltMissing { get; set; }
    public int? MainImageWidth { get; set; }
    public int? MainImageHeight { get; set; }
    public bool HasAggregateRating { get; set; }
    public bool HasVisibleReviewSection { get; set; }
    public bool IsHttps { get; set; }
    public bool HasViewportMeta { get; set; }
    public string? GoogleProductCategory { get; set; }
    public bool HasShippingDetails { get; set; }
    public bool HasReturnPolicy { get; set; }
    public string? PriceValidUntil { get; set; }
    public bool HasSalePrice { get; set; }
    public bool UrlHasTrackingParams { get; set; }
    public bool ImageLooksPromotional { get; set; }

    public static ExtractedProductData? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
        try
        {
            return JsonSerializer.Deserialize<ExtractedProductData>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class GmcRuleDefinition
{
    public string Id { get; set; } = "";
    public string Field { get; set; } = "";
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = "";
    public string FixHint { get; set; } = "";
    public string? DocUrl { get; set; }
    public IList<string>? Patterns { get; set; }
}

public sealed class GmcValidationIssue
{
    public string RuleId { get; set; } = "";
    public string Field { get; set; } = "";
    public ProductComplianceIssueSeverity Severity { get; set; }
    public ProductComplianceIssueSource Source { get; set; }
    public string Message { get; set; } = "";
    public string FixHint { get; set; } = "";
    public string? DocUrl { get; set; }
    public string? Evidence { get; set; }
    public string? GmcIssueCode { get; set; }
}

public sealed class PriorityAction
{
    public string RuleId { get; set; } = "";
    public string Message { get; set; } = "";
    public string FixHint { get; set; } = "";
    public int AffectedCount { get; set; }
}
