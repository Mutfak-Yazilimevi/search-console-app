using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.MerchantCenter;

namespace SearchConsoleApp.Services.MerchantCenter;

public partial class ProductComplianceService
{
    private async Task FinalizeAnalysisAsync(ProductComplianceRun run, CancellationToken cancellationToken)
    {
        var items = await _itemRepo.Table.Where(i => i.RunId == run.Id).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            var data = ExtractedProductData.FromJson(item.ExtractedDataJson) ?? new ExtractedProductData { Url = item.PageUrl };
            var validationIssues = _validator.ValidateProduct(data, item.PageUrl);

            foreach (var vi in validationIssues)
            {
                await _issueRepo.InsertAsync(new ProductComplianceIssue
                {
                    RunId = run.Id,
                    ItemId = item.Id,
                    PageUrl = item.PageUrl,
                    RuleId = vi.RuleId,
                    Field = vi.Field,
                    Severity = vi.Severity,
                    Source = vi.Source,
                    Message = vi.Message,
                    FixHint = vi.FixHint,
                    DocUrl = vi.DocUrl,
                    Evidence = vi.Evidence,
                    CreatedAt = now,
                }, publishEvent: false);
            }

            var itemIssues = await _issueRepo.Table.Where(i => i.ItemId == item.Id).ToListAsync(cancellationToken);
            item.ComplianceScore = GmcComplianceScoreCalculator.CalculateItemScore(
                itemIssues.Select(i => new GmcValidationIssue
                {
                    RuleId = i.RuleId,
                    Severity = i.Severity,
                }));
            item.Status = GmcComplianceScoreCalculator.ClassifyItem(item.ComplianceScore);
            item.IssueCount = itemIssues.Count;
            if (string.IsNullOrWhiteSpace(item.Title)) item.Title = data.Name;
            await _itemRepo.UpdateAsync(item, publishEvent: false);
        }

        var siteValidation = _validator.ValidateSite(run.SiteCheckHtml);
        foreach (var si in siteValidation)
        {
            await _issueRepo.InsertAsync(new ProductComplianceIssue
            {
                RunId = run.Id,
                RuleId = si.RuleId,
                Field = si.Field,
                Severity = si.Severity,
                Source = si.Source,
                Message = si.Message,
                FixHint = si.FixHint,
                DocUrl = si.DocUrl,
                Evidence = si.Evidence,
                CreatedAt = now,
            }, publishEvent: false);
        }

        var productData = items
            .Select(i =>
            {
                var data = ExtractedProductData.FromJson(i.ExtractedDataJson)
                    ?? new ExtractedProductData { Url = i.PageUrl };
                if (string.IsNullOrWhiteSpace(data.Url)) data.Url = i.PageUrl;
                if (string.IsNullOrWhiteSpace(data.Name) && !string.IsNullOrWhiteSpace(i.Title))
                    data.Name = i.Title;
                return (i.Id, i.PageUrl, data);
            })
            .ToList();

        foreach (var ci in _validator.ValidateCrossProducts(productData))
        {
            await _issueRepo.InsertAsync(new ProductComplianceIssue
            {
                RunId = run.Id,
                RuleId = ci.RuleId,
                Field = ci.Field,
                Severity = ci.Severity,
                Source = ci.Source,
                Message = ci.Message,
                FixHint = ci.FixHint,
                DocUrl = ci.DocUrl,
                Evidence = ci.Evidence,
                CreatedAt = now,
            }, publishEvent: false);
        }

        run.ProgressMessage = "Safe Browsing kontrol ediliyor…";
        await _runRepo.UpdateAsync(run, publishEvent: false);
        await _safeBrowsingChecker.CheckSiteAsync(run, cancellationToken);

        run.ProgressMessage = "PageSpeed Insights (örnek ürünler)…";
        await _runRepo.UpdateAsync(run, publishEvent: false);
        await _pageSpeedChecker.CheckSampleProductsAsync(run, items, cancellationToken);
        await RecalculateItemScoresAsync(items, cancellationToken);

        if (run.AnalysisMode == ProductComplianceAnalysisMode.GmcConnected
            && run.CustomerId.HasValue
            && !string.IsNullOrWhiteSpace(run.MerchantCenterAccountId))
        {
            await MergeGmcFeedDataAsync(run, now, cancellationToken);
        }

        items = await _itemRepo.Table.Where(i => i.RunId == run.Id).ToListAsync(cancellationToken);
        await RecalculateRunTotalsAsync(run, cancellationToken);

        run.Status = ProductComplianceRunStatus.Completed;
        run.ProgressPhase = "completed";
        run.ProgressMessage = "Analiz tamamlandı";
        run.CompletedAt = DateTime.UtcNow;
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    private async Task ReanalyzeProductAsync(
        ProductComplianceRun run,
        ProductComplianceItem item,
        CancellationToken cancellationToken)
    {
        var existingIssues = await _issueRepo.Table
            .Where(i => i.ItemId == item.Id
                && i.Source != ProductComplianceIssueSource.MerchantCenter)
            .ToListAsync(cancellationToken);

        foreach (var issue in existingIssues)
            await _issueRepo.DeleteAsync(issue, publishEvent: false);

        var data = ExtractedProductData.FromJson(item.ExtractedDataJson)
            ?? new ExtractedProductData { Url = item.PageUrl };
        var now = DateTime.UtcNow;

        foreach (var vi in _validator.ValidateProduct(data, item.PageUrl))
        {
            await _issueRepo.InsertAsync(new ProductComplianceIssue
            {
                RunId = run.Id,
                ItemId = item.Id,
                PageUrl = item.PageUrl,
                RuleId = vi.RuleId,
                Field = vi.Field,
                Severity = vi.Severity,
                Source = vi.Source,
                Message = vi.Message,
                FixHint = vi.FixHint,
                DocUrl = vi.DocUrl,
                Evidence = vi.Evidence,
                CreatedAt = now,
            }, publishEvent: false);
        }

        if (string.IsNullOrWhiteSpace(item.Title)) item.Title = data.Name;
        await RecalculateItemScoresAsync([item], cancellationToken);
    }

    private async Task RecalculateRunTotalsAsync(
        ProductComplianceRun run,
        CancellationToken cancellationToken)
    {
        var items = await _itemRepo.Table.Where(i => i.RunId == run.Id).ToListAsync(cancellationToken);
        run.CompliantCount = items.Count(i => i.Status == ProductComplianceItemStatus.Compliant);
        run.PartialCount = items.Count(i => i.Status == ProductComplianceItemStatus.Partial);
        run.NonCompliantCount = items.Count(i => i.Status == ProductComplianceItemStatus.NonCompliant);
        run.TotalProducts = items.Count;

        var allIssues = await _issueRepo.Table.Where(i => i.RunId == run.Id).ToListAsync(cancellationToken);
        run.CriticalCount = allIssues.Count(i => i.Severity == ProductComplianceIssueSeverity.Critical);
        run.WarningCount = allIssues.Count(i => i.Severity == ProductComplianceIssueSeverity.Warning);
        run.InfoCount = allIssues.Count(i => i.Severity == ProductComplianceIssueSeverity.Info);

        var siteIssueCount = allIssues.Count(i =>
            i.Source is ProductComplianceIssueSource.SiteLevel or ProductComplianceIssueSource.SafeBrowsing);
        run.SiteReadinessScore = siteIssueCount == 0 ? 100 : Math.Max(0, 100 - siteIssueCount * 12);

        run.ComplianceScore = GmcComplianceScoreCalculator.CalculateRunScore(
            run.CompliantCount, run.PartialCount, run.NonCompliantCount, run.SiteReadinessScore);

        var priorities = GmcComplianceScoreCalculator.BuildPriorityActions(allIssues);
        run.PriorityActionsJson = JsonSerializer.Serialize(priorities);
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    private async Task RecalculateItemScoresAsync(
        IList<ProductComplianceItem> items,
        CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            var itemIssues = await _issueRepo.Table.Where(i => i.ItemId == item.Id).ToListAsync(cancellationToken);
            item.ComplianceScore = GmcComplianceScoreCalculator.CalculateItemScore(
                itemIssues.Select(i => new GmcValidationIssue
                {
                    RuleId = i.RuleId,
                    Severity = i.Severity,
                }));
            item.Status = GmcComplianceScoreCalculator.ClassifyItem(item.ComplianceScore);
            item.IssueCount = itemIssues.Count;
            await _itemRepo.UpdateAsync(item, publishEvent: false);
        }
    }

    private async Task MergeGmcFeedDataAsync(
        ProductComplianceRun run,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await _gmcAuth.GetAccessTokenAsync(run.CustomerId!.Value, cancellationToken);
            if (token == null)
            {
                await InsertGmcWarningAsync(
                    run,
                    "Merchant Center erişim token'ı alınamadı. OAuth bağlantısını yenileyin; site analizi sonuçları korunuyor.",
                    now,
                    cancellationToken);
                return;
            }

            var feed = await _gmcApi.FetchProductsAsync(token, run.MerchantCenterAccountId!, cancellationToken);
            var aggregate = await _gmcApi.FetchAggregateProductStatusesAsync(
                token, run.MerchantCenterAccountId!, cancellationToken);
            var accountIssues = await _gmcApi.FetchAccountIssuesAsync(
                token, run.MerchantCenterAccountId!, cancellationToken);
            var performance = await _gmcApi.FetchProductPerformanceAsync(
                token, run.MerchantCenterAccountId!, limit: 10, cancellationToken);

            run.GmcSummaryJson = JsonSerializer.Serialize(
                new GmcRunSummary(aggregate, accountIssues, performance),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            foreach (var accountIssue in accountIssues)
            {
                await _issueRepo.InsertAsync(new ProductComplianceIssue
                {
                    RunId = run.Id,
                    RuleId = "GMC-ACCOUNT-ISSUE",
                    Field = "account",
                    Severity = MapAccountIssueSeverity(accountIssue.Severity),
                    Source = ProductComplianceIssueSource.MerchantCenter,
                    Message = accountIssue.Title,
                    FixHint = GmcIssueFixHintMapper.Map(
                        accountIssue.Name,
                        accountIssue.Detail ?? accountIssue.Title,
                        "Merchant Center hesap sorununu giderin."),
                    DocUrl = "https://support.google.com/merchants/?hl=tr",
                    Evidence = accountIssue.Detail,
                    CreatedAt = now,
                }, publishEvent: false);
            }

            var refreshedItems = await _itemRepo.Table.Where(i => i.RunId == run.Id).ToListAsync(cancellationToken);
            await _gmcMerge.MergeAsync(run, refreshedItems, feed, cancellationToken);

            foreach (var item in refreshedItems)
            {
                var count = await _issueRepo.Table.CountAsync(i => i.ItemId == item.Id, cancellationToken);
                item.IssueCount = count;
                await _itemRepo.UpdateAsync(item, publishEvent: false);
            }

            if (feed.Count == 0 && aggregate.Count == 0 && accountIssues.Count == 0)
            {
                await InsertGmcWarningAsync(
                    run,
                    "Merchant Center API yanıt vermedi veya hesapta ürün bulunamadı. registerGcp ve API erişimini kontrol edin.",
                    now,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GMC merge failed for run {EntityId}", run.EntityId);
            await InsertGmcWarningAsync(
                run,
                "Merchant Center API hatası — site analizi sonuçları korunuyor. Bağlantıyı ve GCP kaydını kontrol edin.",
                now,
                cancellationToken);
        }
    }

    private async Task InsertGmcWarningAsync(
        ProductComplianceRun run,
        string message,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _issueRepo.InsertAsync(new ProductComplianceIssue
        {
            RunId = run.Id,
            RuleId = "GMC-WARN-001",
            Field = "gmc-api",
            Severity = ProductComplianceIssueSeverity.Warning,
            Source = ProductComplianceIssueSource.MerchantCenter,
            Message = message,
            FixHint = "Merchant Center OAuth bağlantısını yenileyin, Merchant API'nin etkin olduğundan ve registerGcp yapıldığından emin olun.",
            DocUrl = "https://support.google.com/merchants/?hl=tr",
            CreatedAt = now,
        }, publishEvent: false);
    }

    private static ProductComplianceIssueSeverity MapAccountIssueSeverity(string? severity)
    {
        if (severity != null && severity.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            return ProductComplianceIssueSeverity.Critical;
        if (severity != null && severity.Contains("WARNING", StringComparison.OrdinalIgnoreCase))
            return ProductComplianceIssueSeverity.Warning;
        return ProductComplianceIssueSeverity.Info;
    }
}
