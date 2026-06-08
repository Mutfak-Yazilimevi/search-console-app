using SearchConsoleApp.Core.Domain.MerchantCenter;

namespace SearchConsoleApp.Services.MerchantCenter;

public partial class GmcSpecValidator
{
    public IList<GmcValidationIssue> ValidateSite(string? combinedHtml)
    {
        var issues = new List<GmcValidationIssue>();
        if (string.IsNullOrWhiteSpace(combinedHtml))
        {
            foreach (var rule in _rules.GetSiteRules())
            {
                issues.Add(MapSiteRule(rule, "site-html-missing"));
            }
            return issues;
        }

        var lower = combinedHtml.ToLowerInvariant();
        foreach (var rule in _rules.GetSiteRules())
        {
            var patterns = rule.Patterns ?? [];
            if (patterns.Any(p => lower.Contains(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            issues.Add(MapSiteRule(rule, "pattern-not-found"));
        }

        if (!lower.Contains("sitemap", StringComparison.OrdinalIgnoreCase)
            || (!lower.Contains("product", StringComparison.OrdinalIgnoreCase)
                && !lower.Contains("image", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new GmcValidationIssue
            {
                RuleId = "GMC-X-005",
                Field = "sitemap",
                Severity = ProductComplianceIssueSeverity.Info,
                Source = ProductComplianceIssueSource.CrossProduct,
                Message = "Ürün XML sitemap referansı bulunamadı.",
                FixHint = "Ürün URL'lerini product/image XML sitemap'e ekleyin.",
                DocUrl = "https://support.google.com/merchants/answer/7439058?hl=tr",
                Evidence = "no-product-sitemap-hint",
            });
        }

        return issues;
    }

    private static GmcValidationIssue MapSiteRule(GmcRuleDefinition rule, string evidence) => new()
    {
        RuleId = rule.Id,
        Field = rule.Field,
        Severity = ParseSeverity(rule.Severity),
        Source = ProductComplianceIssueSource.SiteLevel,
        Message = rule.Message,
        FixHint = rule.FixHint,
        DocUrl = rule.DocUrl,
        Evidence = evidence,
    };
}
