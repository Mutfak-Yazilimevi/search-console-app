using SearchConsoleApp.Core.Domain.MerchantCenter;

namespace SearchConsoleApp.Services.MerchantCenter;

public partial class GmcSpecValidator
{
    public IList<GmcValidationIssue> ValidateCrossProducts(IList<(long ItemId, string PageUrl, ExtractedProductData Data)> products)
    {
        var issues = new List<GmcValidationIssue>();

        var titleGroups = products
            .Where(p => !string.IsNullOrWhiteSpace(p.Data.Name))
            .GroupBy(p => p.Data.Name!.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1);

        foreach (var group in titleGroups)
        {
            var title = group.First().Data.Name!.Trim();
            var entries = DistinctProductEntries(group);

            issues.Add(new GmcValidationIssue
            {
                RuleId = "GMC-X-001",
                Field = "title",
                Severity = ProductComplianceIssueSeverity.Warning,
                Source = ProductComplianceIssueSource.CrossProduct,
                Message = $"Aynı başlık \"{TruncateEvidence(title, 80)}\" {Math.Max(entries.Count, group.Count())} farklı URL'de kullanılıyor.",
                FixHint = "Her varyant/ürün için benzersiz title kullanın.",
                DocUrl = "https://support.google.com/merchants/answer/6324405?hl=tr",
                Evidence = BuildTitleEvidence(title, entries),
            });
        }

        var descGroups = products
            .Where(p => !string.IsNullOrWhiteSpace(p.Data.Description) && p.Data.Description!.Length > 50)
            .GroupBy(p => p.Data.Description!.Trim().ToLowerInvariant())
            .Where(g => g.Count() > 1);

        foreach (var group in descGroups)
        {
            var description = group.First().Data.Description!.Trim();
            var entries = DistinctProductEntries(group);

            issues.Add(new GmcValidationIssue
            {
                RuleId = "GMC-X-004",
                Field = "description",
                Severity = ProductComplianceIssueSeverity.Info,
                Source = ProductComplianceIssueSource.CrossProduct,
                Message = $"Aynı açıklama \"{TruncateEvidence(description, 80)}\" {Math.Max(entries.Count, group.Count())} üründe tekrarlanıyor.",
                FixHint = "Her ürüne benzersiz açıklama yazın.",
                DocUrl = "https://support.google.com/merchants/answer/6324468?hl=tr",
                Evidence = BuildDescriptionEvidence(description, entries),
            });
        }

        var gtinGroups = products
            .Where(p => !string.IsNullOrWhiteSpace(p.Data.Gtin))
            .GroupBy(p => p.Data.Gtin!.Trim())
            .Where(g => g.Select(x => NormalizeUrl(ResolveProductUrl(x.PageUrl, x.Data))).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);

        foreach (var group in gtinGroups)
        {
            issues.Add(new GmcValidationIssue
            {
                RuleId = "GMC-X-002",
                Field = "gtin",
                Severity = ProductComplianceIssueSeverity.Critical,
                Source = ProductComplianceIssueSource.CrossProduct,
                Message = $"Aynı GTIN {group.Count()} farklı URL'de kullanılıyor.",
                FixHint = "Aynı GTIN'i yalnızca bir ürün URL'sinde kullanın.",
                DocUrl = "https://support.google.com/merchants/answer/6324461?hl=tr",
                Evidence = group.Key,
            });
        }

        var skuPriceGroups = products
            .Where(p => !string.IsNullOrWhiteSpace(p.Data.Sku) && p.Data.SchemaPrice != null)
            .GroupBy(p => p.Data.Sku!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(x => x.Data.SchemaPrice).Distinct().Count() > 1);

        foreach (var group in skuPriceGroups)
        {
            issues.Add(new GmcValidationIssue
            {
                RuleId = "GMC-X-003",
                Field = "sku",
                Severity = ProductComplianceIssueSeverity.Warning,
                Source = ProductComplianceIssueSource.CrossProduct,
                Message = $"Aynı SKU ({group.Key}) için farklı fiyatlar bulundu.",
                FixHint = "SKU başına tek güncel fiyat tutun.",
                DocUrl = "https://support.google.com/merchants/answer/6324371?hl=tr",
                Evidence = group.Key,
            });
        }

        return issues;
    }

    private static string ResolveProductUrl(string pageUrl, ExtractedProductData data)
        => !string.IsNullOrWhiteSpace(data.Url) ? data.Url.Trim() : pageUrl.Trim();

    private static string TruncateEvidence(string value, int maxLength)
    {
        if (value.Length <= maxLength) return value;
        return value[..(maxLength - 1)] + "…";
    }

    private sealed record ProductEvidenceEntry(
        string Url,
        string? Title,
        string? Sku = null,
        string? Color = null,
        string? Size = null);

    private static List<ProductEvidenceEntry> DistinctProductEntries(
        IEnumerable<(long ItemId, string PageUrl, ExtractedProductData Data)> items)
        => items
            .Select(x => new ProductEvidenceEntry(
                ResolveProductUrl(x.PageUrl, x.Data),
                string.IsNullOrWhiteSpace(x.Data.Name) ? null : x.Data.Name.Trim(),
                string.IsNullOrWhiteSpace(x.Data.Sku) ? null : x.Data.Sku.Trim(),
                string.IsNullOrWhiteSpace(x.Data.Color) ? null : x.Data.Color.Trim(),
                string.IsNullOrWhiteSpace(x.Data.Size) ? null : x.Data.Size.Trim()))
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

    private static string BuildTitleEvidence(string title, IList<ProductEvidenceEntry> products)
    {
        var lines = new List<string> { $"Başlık: \"{title}\"" };
        lines.AddRange(products.Select(FormatTitleProductLine));
        return string.Join('\n', lines);
    }

    private static string BuildDescriptionEvidence(string description, IList<ProductEvidenceEntry> products)
    {
        var preview = TruncateEvidence(description, 120);
        var lines = new List<string> { $"Açıklama: \"{preview}\"" };
        lines.AddRange(products.Select(FormatDescriptionProductLine));
        return string.Join('\n', lines);
    }

    private static string FormatTitleProductLine(ProductEvidenceEntry product)
    {
        var suffix = FormatVariantSuffix(product);
        return string.IsNullOrWhiteSpace(suffix) ? product.Url : $"{product.Url} ({suffix})";
    }

    private static string FormatDescriptionProductLine(ProductEvidenceEntry product)
    {
        var suffix = FormatVariantSuffix(product);
        if (!string.IsNullOrWhiteSpace(product.Title))
        {
            return string.IsNullOrWhiteSpace(suffix)
                ? $"{product.Title} — {product.Url}"
                : $"{product.Title} — {product.Url} ({suffix})";
        }

        return string.IsNullOrWhiteSpace(suffix) ? product.Url : $"{product.Url} ({suffix})";
    }

    private static string? FormatVariantSuffix(ProductEvidenceEntry product)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(product.Sku)) parts.Add($"SKU: {product.Sku}");
        if (!string.IsNullOrWhiteSpace(product.Color)) parts.Add($"Renk: {product.Color}");
        if (!string.IsNullOrWhiteSpace(product.Size)) parts.Add($"Beden: {product.Size}");
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }
}
