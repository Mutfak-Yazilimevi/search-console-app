using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.MerchantCenter;

public interface IGmcSpecValidator
{
    IList<GmcValidationIssue> ValidateProduct(ExtractedProductData data, string pageUrl);
    IList<GmcValidationIssue> ValidateSite(string? combinedHtml);
    IList<GmcValidationIssue> ValidateCrossProducts(IList<(long ItemId, string PageUrl, ExtractedProductData Data)> products);
}

public partial class GmcSpecValidator : IGmcSpecValidator, IScopedService
{
    private readonly IGmcRulesLoader _rules;

    public GmcSpecValidator(IGmcRulesLoader rules) => _rules = rules;

    public IList<GmcValidationIssue> ValidateProduct(ExtractedProductData data, string pageUrl)
    {
        var issues = new List<GmcValidationIssue>();
        var rules = _rules.GetProductSpecRules().ToDictionary(r => r.Id);

        void Add(string ruleId, string? evidence = null)
        {
            if (!rules.TryGetValue(ruleId, out var rule)) return;
            issues.Add(new GmcValidationIssue
            {
                RuleId = rule.Id,
                Field = rule.Field,
                Severity = ParseSeverity(rule.Severity),
                Source = ProductComplianceIssueSource.SpecValidation,
                Message = rule.Message,
                FixHint = rule.FixHint,
                DocUrl = rule.DocUrl,
                Evidence = evidence,
            });
        }

        if (string.IsNullOrWhiteSpace(data.Sku) && string.IsNullOrWhiteSpace(data.ProductId))
            Add("GMC-SPEC-001");

        if (string.IsNullOrWhiteSpace(data.Name) || (data.Name?.Length ?? 0) > 150)
            Add("GMC-SPEC-002", data.Name?.Length.ToString());

        if (string.IsNullOrWhiteSpace(data.Description) || (data.Description?.Trim().Length ?? 0) < 100)
            Add("GMC-SPEC-003", $"length={(data.Description?.Length ?? 0)}");

        if (data.IsNoindex || !data.IsHttps)
            Add("GMC-SPEC-004", data.IsNoindex ? "noindex" : "not-https");

        if (string.IsNullOrWhiteSpace(data.Image) && data.Images.Count == 0)
            Add("GMC-SPEC-005");
        else if (!string.IsNullOrWhiteSpace(data.Image) && data.Image.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            Add("GMC-SPEC-005", "http-image");

        if (data.SchemaPrice == null || data.SchemaPrice <= 0)
            Add("GMC-SPEC-006");

        if (string.IsNullOrWhiteSpace(data.Availability))
            Add("GMC-SPEC-007");

        if (string.IsNullOrWhiteSpace(data.Brand))
            Add("GMC-SPEC-008");

        if (string.IsNullOrWhiteSpace(data.Gtin) && string.IsNullOrWhiteSpace(data.Mpn))
            Add("GMC-SPEC-009");

        if (string.IsNullOrWhiteSpace(data.Condition))
            Add("GMC-SPEC-010");

        if (!data.HasProductSchema)
            Add("GMC-SPEC-011");

        if (data.SchemaPrice != null && data.VisiblePrice != null
            && Math.Abs(data.SchemaPrice.Value - data.VisiblePrice.Value) > 0.01m)
            Add("GMC-SPEC-012", FormatPriceMismatchEvidence(data.SchemaPrice, data.VisiblePrice, data.PriceCurrency));

        if (!string.IsNullOrWhiteSpace(data.PriceCurrency)
            && !string.Equals(data.PriceCurrency, "TRY", StringComparison.OrdinalIgnoreCase))
            Add("GMC-SPEC-014", data.PriceCurrency);

        var imageCount = data.ImageCount > 0
            ? data.ImageCount
            : data.Images.Count + (string.IsNullOrWhiteSpace(data.Image) ? 0 : 1);

        if (data.VisiblePrice != null && data.SchemaPrice != null
            && data.VisiblePrice < data.SchemaPrice * 0.99m
            && data.SchemaListPrice == null)
            Add("GMC-SPEC-013", FormatPriceMismatchEvidence(data.SchemaPrice, data.VisiblePrice, data.PriceCurrency, data.SchemaListPrice));

        if (imageCount < 2)
            Add("GMC-SPEC-015", $"count={imageCount}");

        if (( !string.IsNullOrWhiteSpace(data.Color) || !string.IsNullOrWhiteSpace(data.Size))
            && string.IsNullOrWhiteSpace(data.ItemGroupId))
            Add("GMC-SPEC-018");

        if (!string.IsNullOrWhiteSpace(data.Sku) && !string.IsNullOrWhiteSpace(data.VisibleSku)
            && !string.Equals(data.Sku.Trim(), data.VisibleSku.Trim(), StringComparison.OrdinalIgnoreCase))
            Add("GMC-SPEC-021", $"schema={data.Sku}, visible={data.VisibleSku}");

        if (!data.HasProductSchema && data.HasOgProductMeta)
            Add("GMC-SPEC-022");

        if (data.CanonicalPointsElsewhere)
            Add("GMC-SPEC-027", data.CanonicalUrl);

        if (data.MainImageAltMissing)
            Add("GMC-SPEC-029");

        if (data.MainImageWidth is > 0 && data.MainImageHeight is > 0)
        {
            if (data.MainImageWidth < 100 || data.MainImageHeight < 100)
                Add("GMC-SPEC-016", $"{data.MainImageWidth}x{data.MainImageHeight}");
            else if (data.MainImageWidth < 800 || data.MainImageHeight < 800)
                Add("GMC-SPEC-016", $"recommended-800, actual={data.MainImageWidth}x{data.MainImageHeight}");
        }

        if (data.HasAggregateRating && !data.HasVisibleReviewSection)
            Add("GMC-SPEC-024");

        if (data.IdentifierExists == false
            && (!string.IsNullOrWhiteSpace(data.Gtin) || !string.IsNullOrWhiteSpace(data.Mpn)))
            Add("GMC-SPEC-017", "identifier_exists=false");

        if (TitleHasPromoSpam(data.Name))
            Add("GMC-SPEC-019", data.Name);

        if (!data.HasShippingDetails && !data.HasReturnPolicy)
            Add("GMC-SPEC-020");

        if (data.UrlHasTrackingParams)
            Add("GMC-SPEC-023", pageUrl);

        if (data.SchemaPrice is > 0 and < 1)
            Add("GMC-SPEC-025", data.SchemaPrice?.ToString());

        if (string.IsNullOrWhiteSpace(data.GoogleProductCategory))
            Add("GMC-SPEC-026");

        if (!data.HasViewportMeta)
            Add("GMC-SPEC-028");

        if (TitleMostlyCaps(data.Name))
            Add("GMC-SPEC-030", data.Name);

        if (data.HasSalePrice && string.IsNullOrWhiteSpace(data.PriceValidUntil))
            Add("GMC-SPEC-032");

        if (data.ImageLooksPromotional)
            Add("GMC-SPEC-031", data.Image);

        return issues;
    }
}
