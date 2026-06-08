using FluentAssertions;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Services.MerchantCenter;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

public class GmcComplianceScoreCalculatorTests
{
    [Fact]
    public void CalculateRunScore_weights_site_readiness()
    {
        var score = GmcComplianceScoreCalculator.CalculateRunScore(
            compliantCount: 8,
            partialCount: 2,
            nonCompliantCount: 0,
            siteScore: 40);

        score.Should().BeInRange(70, 90);
    }

    [Fact]
    public void ClassifyItem_maps_score_bands()
    {
        GmcComplianceScoreCalculator.ClassifyItem(95).Should().Be(ProductComplianceItemStatus.Compliant);
        GmcComplianceScoreCalculator.ClassifyItem(70).Should().Be(ProductComplianceItemStatus.Partial);
        GmcComplianceScoreCalculator.ClassifyItem(30).Should().Be(ProductComplianceItemStatus.NonCompliant);
    }

    [Fact]
    public void BuildPriorityActions_groups_spec_issues_by_rule()
    {
        var issues = new List<ProductComplianceIssue>
        {
            new() { RuleId = "GMC-SPEC-002", ItemId = 1, Message = "a", FixHint = "b", Source = ProductComplianceIssueSource.SpecValidation },
            new() { RuleId = "GMC-SPEC-002", ItemId = 2, Message = "a", FixHint = "b", Source = ProductComplianceIssueSource.SpecValidation },
            new() { RuleId = "GMC-PERF-001", ItemId = 3, Message = "c", FixHint = "d", Source = ProductComplianceIssueSource.PageSpeed },
        };

        var actions = GmcComplianceScoreCalculator.BuildPriorityActions(issues);

        actions.Should().ContainSingle(a => a.RuleId == "GMC-SPEC-002");
        actions.First(a => a.RuleId == "GMC-SPEC-002").AffectedCount.Should().Be(2);
    }
}
