using SearchConsoleApp.Core.Domain.MerchantCenter;

namespace SearchConsoleApp.Services.MerchantCenter;

public static class GmcComplianceScoreCalculator
{
    public static int CalculateItemScore(IEnumerable<GmcValidationIssue> issues)
    {
        var score = 100;
        foreach (var issue in issues)
        {
            score -= issue.Severity switch
            {
                ProductComplianceIssueSeverity.Critical => 25,
                ProductComplianceIssueSeverity.Warning => 10,
                _ => 3,
            };
        }

        return Math.Max(0, score);
    }

    public static ProductComplianceItemStatus ClassifyItem(int score) => score switch
    {
        >= 90 => ProductComplianceItemStatus.Compliant,
        >= 50 => ProductComplianceItemStatus.Partial,
        _ => ProductComplianceItemStatus.NonCompliant,
    };

    public static int CalculateRunScore(
        int compliantCount,
        int partialCount,
        int nonCompliantCount,
        int? siteScore = null)
    {
        var total = compliantCount + partialCount + nonCompliantCount;
        if (total == 0) return siteScore ?? 0;

        var productScore = (int)Math.Round(
            (compliantCount * 100.0 + partialCount * 70.0 + nonCompliantCount * 30.0) / total);

        if (siteScore == null) return productScore;
        return (int)Math.Round(productScore * 0.85 + siteScore.Value * 0.15);
    }

    public static IList<PriorityAction> BuildPriorityActions(IEnumerable<ProductComplianceIssue> issues)
    {
        return issues
            .Where(i => i.Source is ProductComplianceIssueSource.SpecValidation
                or ProductComplianceIssueSource.CrossProduct)
            .GroupBy(i => i.RuleId)
            .Select(g =>
            {
                var first = g.First();
                return new PriorityAction
                {
                    RuleId = g.Key,
                    Message = first.Message,
                    FixHint = first.FixHint,
                    AffectedCount = g.Count(i => i.ItemId.HasValue),
                };
            })
            .OrderByDescending(p => p.AffectedCount)
            .Take(10)
            .ToList();
    }
}
