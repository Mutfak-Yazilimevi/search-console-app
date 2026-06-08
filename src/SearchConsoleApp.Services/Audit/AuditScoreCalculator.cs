using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Services.Audit;

/// <summary>
/// SEO skoru — benzersiz kural ihlallerine göre hesaplanır.
/// </summary>
public static class AuditScoreCalculator
{
    public static int CalculateFromIssues(IList<AuditIssue> issues)
    {
        if (issues.Count == 0) return 100;

        var distinctByRule = issues
            .GroupBy(i => i.RuleId)
            .Select(g => g.OrderBy(i => i.Severity).First())
            .ToList();

        var critical = distinctByRule.Count(i => i.Severity == AuditIssueSeverity.Critical);
        var warning = distinctByRule.Count(i => i.Severity == AuditIssueSeverity.Warning);
        var info = distinctByRule.Count(i => i.Severity == AuditIssueSeverity.Info);

        var penalty = critical * 15 + warning * 6 + Math.Min(info, 20) * 2;
        return Math.Max(0, 100 - penalty);
    }
}
