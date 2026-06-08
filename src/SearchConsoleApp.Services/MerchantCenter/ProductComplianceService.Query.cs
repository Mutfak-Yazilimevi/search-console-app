using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Domain.MerchantCenter;

namespace SearchConsoleApp.Services.MerchantCenter;

public partial class ProductComplianceService
{
    public async Task<ProductComplianceDetailDto?> GetDetailAsync(Guid entityId, CancellationToken cancellationToken = default)
        => await BuildDetailAsync(entityId, maxProducts: 100, cancellationToken);

    public async Task<IList<ProductComplianceRunSummaryDto>> ListRecentRunsAsync(
        long customerId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var runs = await _runRepo.Table
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return runs.Select(MapRunSummary).ToList();
    }

    public async Task<ProductComplianceDetailDto?> GetExportDetailAsync(Guid entityId, CancellationToken cancellationToken = default)
        => await BuildDetailAsync(entityId, maxProducts: null, cancellationToken);

    public async Task<ProductComplianceProductDetailDto?> GetProductDetailAsync(
        Guid runEntityId,
        Guid productEntityId,
        CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null) return null;

        var item = await _itemRepo.GetByEntityIdAsync(productEntityId);
        if (item == null || item.RunId != run.Id) return null;

        var issues = await _issueRepo.Table
            .Where(i => i.ItemId == item.Id)
            .OrderByDescending(i => i.Severity)
            .ToListAsync(cancellationToken);

        return new ProductComplianceProductDetailDto(MapItem(item), issues.Select(MapIssue).ToList());
    }

    public async Task<IList<ProductComplianceItemDto>> GetProductsAsync(
        Guid entityId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(entityId);
        if (run == null) return [];

        take = Math.Clamp(take, 1, 100);
        var items = await _itemRepo.Table
            .Where(i => i.RunId == run.Id)
            .OrderByDescending(i => i.IssueCount)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return items.Select(MapItem).ToList();
    }

    public async Task<string> ExportJsonAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        var detail = await GetExportDetailAsync(entityId, cancellationToken);
        if (detail == null) return "{}";
        return JsonSerializer.Serialize(detail, new JsonSerializerOptions { WriteIndented = true });
    }

    public Task<string?> ExportHtmlReportAsync(Guid entityId, CancellationToken cancellationToken = default)
        => _reportService.BuildHtmlReportAsync(entityId, cancellationToken);

    private async Task<ProductComplianceDetailDto?> BuildDetailAsync(
        Guid entityId,
        int? maxProducts,
        CancellationToken cancellationToken)
    {
        var run = await _runRepo.GetByEntityIdAsync(entityId);
        if (run == null) return null;

        var productQuery = _itemRepo.Table
            .Where(i => i.RunId == run.Id)
            .OrderByDescending(i => i.IssueCount)
            .ThenBy(i => i.PageUrl);

        var products = maxProducts.HasValue
            ? await productQuery.Take(maxProducts.Value).ToListAsync(cancellationToken)
            : await productQuery.ToListAsync(cancellationToken);

        var siteIssues = await LoadRunLevelIssuesAsync(
            run.Id,
            cancellationToken,
            ProductComplianceIssueSource.SiteLevel,
            ProductComplianceIssueSource.SafeBrowsing);

        var crossProductIssues = await LoadRunLevelIssuesAsync(
            run.Id,
            cancellationToken,
            ProductComplianceIssueSource.CrossProduct);

        var feedIssues = await LoadRunLevelIssuesAsync(
            run.Id,
            cancellationToken,
            ProductComplianceIssueSource.MerchantCenter);

        var comparison = await _diffService.BuildComparisonAsync(run, cancellationToken);

        return new ProductComplianceDetailDto(
            MapRun(run, comparison),
            products.Select(MapItem).ToList(),
            siteIssues.Select(MapIssue).ToList(),
            crossProductIssues.Select(MapIssue).ToList(),
            feedIssues.Select(MapIssue).ToList());
    }

    private async Task<List<ProductComplianceIssue>> LoadRunLevelIssuesAsync(
        long runId,
        CancellationToken cancellationToken,
        params ProductComplianceIssueSource[] sources)
    {
        return await _issueRepo.Table
            .Where(i => i.RunId == runId && i.ItemId == null && sources.Contains(i.Source))
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.RuleId)
            .ToListAsync(cancellationToken);
    }

    private static ProductComplianceRunSummaryDto MapRunSummary(ProductComplianceRun run) => new(
        run.EntityId,
        run.InputUrl,
        run.Status.ToString(),
        run.AnalysisMode.ToString(),
        run.CreatedAt,
        run.CompletedAt,
        run.ComplianceScore,
        run.TotalProducts);

    private static ProductComplianceRunDto MapRun(
        ProductComplianceRun run,
        ProductComplianceComparisonDto? comparison = null)
    {
        IList<PriorityActionDto> priorities = [];
        if (!string.IsNullOrWhiteSpace(run.PriorityActionsJson))
        {
            try
            {
                priorities = JsonSerializer.Deserialize<List<PriorityAction>>(run.PriorityActionsJson)?
                    .Select(p => new PriorityActionDto(p.RuleId, p.Message, p.FixHint, p.AffectedCount))
                    .ToList() ?? [];
            }
            catch { /* ignore */ }
        }

        return new ProductComplianceRunDto(
            run.EntityId,
            run.InputUrl,
            run.NormalizedUrl,
            run.Status.ToString(),
            run.AnalysisMode.ToString(),
            run.CreatedAt,
            run.StartedAt,
            run.CompletedAt,
            run.TotalProducts,
            run.CompliantCount,
            run.PartialCount,
            run.NonCompliantCount,
            run.ComplianceScore,
            run.SiteReadinessScore,
            run.CriticalCount,
            run.WarningCount,
            run.InfoCount,
            run.ErrorMessage,
            run.ProgressPhase,
            run.ProgressMessage,
            run.MerchantCenterAccountId,
            ParseGmcSummary(run.GmcSummaryJson),
            priorities,
            comparison);
    }

    private static GmcRunSummaryDto? ParseGmcSummary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var summary = JsonSerializer.Deserialize<GmcRunSummary>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (summary == null) return null;
            return new GmcRunSummaryDto(
                summary.AggregateStatuses
                    .Select(s => new GmcAggregateStatusDto(
                        s.ReportingContext, s.ApprovedCount, s.PendingCount, s.DisapprovedCount))
                    .ToList(),
                summary.AccountIssues
                    .Select(i => new GmcAccountIssueDto(i.Title, i.Detail, i.Severity))
                    .ToList(),
                (summary.TopPerformance ?? [])
                    .Select(p => new GmcProductPerformanceDto(
                        p.OfferId, p.Title, p.Clicks, p.Impressions, p.ClickThroughRate))
                    .ToList());
        }
        catch
        {
            return null;
        }
    }

    private static ProductComplianceItemDto MapItem(ProductComplianceItem item) => new(
        item.EntityId,
        item.PageUrl,
        item.Title,
        item.OfferId,
        item.GmcStatus,
        item.ComplianceScore,
        item.Status.ToString(),
        item.IssueCount);

    private static ProductComplianceIssueDto MapIssue(ProductComplianceIssue issue) => new(
        issue.EntityId,
        issue.PageUrl,
        issue.RuleId,
        issue.Field,
        issue.Severity.ToString(),
        issue.Source.ToString(),
        issue.Message,
        issue.FixHint,
        issue.DocUrl,
        issue.GmcIssueCode,
        issue.Evidence);
}
