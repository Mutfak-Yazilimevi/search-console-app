using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.MerchantCenter;

public record MerchantCenterAccount(string AccountId, string Name, string? WebsiteUrl);

public record GmcProductRow(
    string OfferId,
    string? Title,
    string? Link,
    string? Brand,
    string? Status,
    IList<GmcItemIssue> Issues);

public record GmcItemIssue(string Code, string Description, string? Severity);

public record GmcAggregateProductStatus(
    string? ReportingContext,
    long ApprovedCount,
    long PendingCount,
    long DisapprovedCount);

public record GmcAccountIssue(string Name, string Title, string? Detail, string? Severity);

public record GmcProductPerformanceRow(
    string OfferId,
    string? Title,
    long Clicks,
    long Impressions,
    double? ClickThroughRate);

public record GmcRunSummary(
    IList<GmcAggregateProductStatus> AggregateStatuses,
    IList<GmcAccountIssue> AccountIssues,
    IList<GmcProductPerformanceRow> TopPerformance);

public interface IMerchantCenterApiClient
{
    Task<IList<MerchantCenterAccount>> ListAccountsAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<IList<GmcProductRow>> FetchProductsAsync(string accessToken, string accountId, CancellationToken cancellationToken = default);
    Task<IList<GmcAggregateProductStatus>> FetchAggregateProductStatusesAsync(
        string accessToken, string accountId, CancellationToken cancellationToken = default);
    Task<IList<GmcAccountIssue>> FetchAccountIssuesAsync(
        string accessToken, string accountId, CancellationToken cancellationToken = default);
    Task<IList<GmcProductPerformanceRow>> FetchProductPerformanceAsync(
        string accessToken, string accountId, int limit = 10, CancellationToken cancellationToken = default);
    string? FindMatchingAccount(IList<MerchantCenterAccount> accounts, string normalizedUrl);
}

public partial class MerchantCenterApiClient : IMerchantCenterApiClient, IScopedService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MerchantCenterApiClient> _logger;

    public MerchantCenterApiClient(IHttpClientFactory httpClientFactory, ILogger<MerchantCenterApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IList<MerchantCenterAccount>> ListAccountsAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var http = CreateClient(accessToken);
        var response = await http.GetAsync("https://merchantapi.googleapis.com/accounts/v1/accounts", cancellationToken);
        if (!response.IsSuccessStatusCode) return [];

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!json.TryGetProperty("accounts", out var accounts)) return [];

        var list = new List<MerchantCenterAccount>();
        foreach (var acc in accounts.EnumerateArray())
        {
            var name = acc.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var accountId = name.Replace("accounts/", "", StringComparison.Ordinal);
            var accountName = acc.TryGetProperty("accountName", out var an) ? an.GetString() ?? accountId : accountId;
            var website = acc.TryGetProperty("websiteUrl", out var w) ? w.GetString() : null;
            if (!string.IsNullOrEmpty(accountId))
                list.Add(new MerchantCenterAccount(accountId, accountName, website));
        }

        return list;
    }

    public string? FindMatchingAccount(IList<MerchantCenterAccount> accounts, string normalizedUrl)
    {
        var uri = new Uri(normalizedUrl);
        var host = uri.Host;

        foreach (var acc in accounts)
        {
            if (string.IsNullOrWhiteSpace(acc.WebsiteUrl)) continue;
            if (Uri.TryCreate(acc.WebsiteUrl, UriKind.Absolute, out var siteUri)
                && string.Equals(siteUri.Host, host, StringComparison.OrdinalIgnoreCase))
                return acc.AccountId;
        }

        return accounts.FirstOrDefault()?.AccountId;
    }

    public async Task<IList<GmcProductRow>> FetchProductsAsync(
        string accessToken,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        var http = CreateClient(accessToken);
        var query = """
            SELECT offer_id, title, link, brand, aggregated_reporting_context_status, item_issues
            FROM product_view
            LIMIT 500
            """;

        var payload = new { query };
        var url = $"https://merchantapi.googleapis.com/reports/v1/accounts/{accountId}/reports:search";
        var response = await http.PostAsJsonAsync(url, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GMC product_view search failed: {Status}", (int)response.StatusCode);
            return [];
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!json.TryGetProperty("results", out var results)) return [];

        var rows = new List<GmcProductRow>();
        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty("productView", out var pv)) continue;

            var offerId = pv.TryGetProperty("offerId", out var o) ? o.GetString() ?? "" : "";
            var title = pv.TryGetProperty("title", out var t) ? t.GetString() : null;
            var link = pv.TryGetProperty("link", out var l) ? l.GetString() : null;
            var brand = pv.TryGetProperty("brand", out var b) ? b.GetString() : null;
            var status = pv.TryGetProperty("aggregatedReportingContextStatus", out var s) ? s.GetString() : null;

            var issues = new List<GmcItemIssue>();
            if (pv.TryGetProperty("itemIssues", out var issueArr))
            {
                foreach (var issue in issueArr.EnumerateArray())
                {
                    var code = issue.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
                    var desc = issue.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    var sev = issue.TryGetProperty("severity", out var sv) ? sv.GetString() : null;
                    if (!string.IsNullOrEmpty(desc))
                        issues.Add(new GmcItemIssue(code, desc, sev));
                }
            }

            rows.Add(new GmcProductRow(offerId, title, link, brand, status, issues));
        }

        return rows;
    }

    public async Task<IList<GmcAggregateProductStatus>> FetchAggregateProductStatusesAsync(
        string accessToken,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        var http = CreateClient(accessToken);
        var url = $"https://merchantapi.googleapis.com/accounts/v1/accounts/{accountId}/aggregateProductStatuses";
        var response = await http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GMC aggregateProductStatuses failed: {Status}", (int)response.StatusCode);
            return [];
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!json.TryGetProperty("aggregateProductStatuses", out var arr)) return [];

        var list = new List<GmcAggregateProductStatus>();
        foreach (var item in arr.EnumerateArray())
        {
            var context = item.TryGetProperty("reportingContext", out var rc) ? rc.GetString() : null;
            list.Add(new GmcAggregateProductStatus(
                context,
                ParseLong(item, "approvedCount"),
                ParseLong(item, "pendingCount"),
                ParseLong(item, "disapprovedCount")));
        }

        return list;
    }

    public async Task<IList<GmcAccountIssue>> FetchAccountIssuesAsync(
        string accessToken,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        var http = CreateClient(accessToken);
        var url = $"https://merchantapi.googleapis.com/accounts/v1/accounts/{accountId}/accountIssues";
        var response = await http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GMC accountIssues failed: {Status}", (int)response.StatusCode);
            return [];
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!json.TryGetProperty("accountIssues", out var arr)) return [];

        var list = new List<GmcAccountIssue>();
        foreach (var item in arr.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? name : name;
            var detail = item.TryGetProperty("detail", out var d) ? d.GetString() : null;
            var severity = item.TryGetProperty("severity", out var s) ? s.GetString() : null;
            if (!string.IsNullOrWhiteSpace(title))
                list.Add(new GmcAccountIssue(name, title, detail, severity));
        }

        return list;
    }

    public async Task<IList<GmcProductPerformanceRow>> FetchProductPerformanceAsync(
        string accessToken,
        string accountId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 50);
        var http = CreateClient(accessToken);
        var query = $"""
            SELECT offer_id, title, clicks, impressions, click_through_rate
            FROM product_performance_view
            WHERE date DURING LAST_30_DAYS
            ORDER BY clicks DESC
            LIMIT {limit}
            """;

        var payload = new { query };
        var url = $"https://merchantapi.googleapis.com/reports/v1/accounts/{accountId}/reports:search";
        var response = await http.PostAsJsonAsync(url, payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GMC product_performance_view search failed: {Status}", (int)response.StatusCode);
            return [];
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!json.TryGetProperty("results", out var results)) return [];

        var rows = new List<GmcProductPerformanceRow>();
        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty("productPerformanceView", out var pv)) continue;

            var offerId = pv.TryGetProperty("offerId", out var o) ? o.GetString() ?? "" : "";
            var title = pv.TryGetProperty("title", out var t) ? t.GetString() : null;
            var clicks = ParseLong(pv, "clicks");
            var impressions = ParseLong(pv, "impressions");
            double? ctr = null;
            if (pv.TryGetProperty("clickThroughRate", out var ctrEl))
            {
                if (ctrEl.ValueKind == JsonValueKind.Number && ctrEl.TryGetDouble(out var d)) ctr = d;
                else if (ctrEl.ValueKind == JsonValueKind.String
                    && double.TryParse(ctrEl.GetString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    ctr = parsed;
            }

            if (!string.IsNullOrEmpty(offerId))
                rows.Add(new GmcProductPerformanceRow(offerId, title, clicks, impressions, ctr));
        }

        return rows;
    }

    private static long ParseLong(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var el)) return 0;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String && long.TryParse(el.GetString(), out var parsed)) return parsed;
        return 0;
    }

    private HttpClient CreateClient(string accessToken)
    {
        var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return http;
    }
}

public interface IGmcProductMergeService
{
    Task MergeAsync(
        ProductComplianceRun run,
        IList<ProductComplianceItem> items,
        IList<GmcProductRow> feedProducts,
        CancellationToken cancellationToken = default);
}

public partial class GmcProductMergeService : IGmcProductMergeService, IScopedService
{
    private readonly IRepository<ProductComplianceIssue> _issueRepo;

    public GmcProductMergeService(IRepository<ProductComplianceIssue> issueRepo) => _issueRepo = issueRepo;

    public async Task MergeAsync(
        ProductComplianceRun run,
        IList<ProductComplianceItem> items,
        IList<GmcProductRow> feedProducts,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var itemByUrl = items.ToDictionary(i => NormalizeUrl(i.PageUrl), i => i, StringComparer.OrdinalIgnoreCase);
        var matchedFeedLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var feed in feedProducts)
        {
            if (string.IsNullOrWhiteSpace(feed.Link)) continue;
            var key = NormalizeUrl(feed.Link);
            matchedFeedLinks.Add(key);

            if (!itemByUrl.TryGetValue(key, out var item))
            {
                await _issueRepo.InsertAsync(new ProductComplianceIssue
                {
                    RunId = run.Id,
                    PageUrl = feed.Link,
                    RuleId = "GMC-FEED-001",
                    Field = "link",
                    Severity = ProductComplianceIssueSeverity.Info,
                    Source = ProductComplianceIssueSource.MerchantCenter,
                    Message = "Ürün feed'de var ancak sitede taranan ürünler arasında bulunamadı.",
                    FixHint = "Ürün URL'sinin sitede erişilebilir olduğundan ve sitemap'te olduğundan emin olun.",
                    DocUrl = "https://support.google.com/merchants/?hl=tr",
                    GmcIssueCode = feed.OfferId,
                    CreatedAt = now,
                }, publishEvent: false);
                continue;
            }

            item.GmcStatus = feed.Status;
            item.OfferId = feed.OfferId;

            foreach (var issue in feed.Issues)
            {
                await _issueRepo.InsertAsync(new ProductComplianceIssue
                {
                    RunId = run.Id,
                    ItemId = item.Id,
                    PageUrl = item.PageUrl,
                    RuleId = "GMC-API-ISSUE",
                    Field = "feed",
                    Severity = MapGmcSeverity(issue.Severity),
                    Source = ProductComplianceIssueSource.MerchantCenter,
                    Message = issue.Description,
                    FixHint = GmcIssueFixHintMapper.Map(
                        issue.Code,
                        issue.Description,
                        "Google Merchant Center'daki bu sorunu gidermek için belirtilen attribute'u düzeltin."),
                    DocUrl = "https://support.google.com/merchants/?hl=tr",
                    GmcIssueCode = issue.Code,
                    CreatedAt = now,
                }, publishEvent: false);
            }
        }

        foreach (var item in items)
        {
            if (matchedFeedLinks.Contains(NormalizeUrl(item.PageUrl))) continue;
            await _issueRepo.InsertAsync(new ProductComplianceIssue
            {
                RunId = run.Id,
                ItemId = item.Id,
                PageUrl = item.PageUrl,
                RuleId = "GMC-FEED-002",
                Field = "feed",
                Severity = ProductComplianceIssueSeverity.Info,
                Source = ProductComplianceIssueSource.MerchantCenter,
                Message = "Sitede bulunan ürün feed'de eşleşmedi.",
                FixHint = "Ürünü Merchant Center feed'ine ekleyin veya link alanının site URL'si ile eşleştiğini doğrulayın.",
                DocUrl = "https://support.google.com/merchants/?hl=tr",
                CreatedAt = now,
            }, publishEvent: false);
        }
    }

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url.Trim().TrimEnd('/');
        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}".TrimEnd('/').ToLowerInvariant();
    }

    private static ProductComplianceIssueSeverity MapGmcSeverity(string? severity)
    {
        if (severity != null && (
            severity.Contains("DISAPPROVED", StringComparison.OrdinalIgnoreCase) ||
            severity.Contains("ERROR", StringComparison.OrdinalIgnoreCase)))
            return ProductComplianceIssueSeverity.Critical;
        return ProductComplianceIssueSeverity.Warning;
    }
}
