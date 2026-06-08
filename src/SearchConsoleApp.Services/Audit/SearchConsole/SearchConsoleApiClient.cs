using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Audit.SearchConsole;

public interface ISearchConsoleApiClient
{
    Task<IList<SearchConsoleProperty>> ListPropertiesAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<SearchConsoleAuditData?> FetchAuditDataAsync(
        string accessToken,
        string propertyUrl,
        IList<string> urlsToInspect,
        CancellationToken cancellationToken = default);
    Task<IList<SearchConsoleSitemapInfo>> ListSitemapsAsync(
        string accessToken,
        string propertyUrl,
        CancellationToken cancellationToken = default);
    string? FindMatchingProperty(IList<SearchConsoleProperty> properties, string normalizedAuditUrl);
}

public partial class SearchConsoleApiClient : ISearchConsoleApiClient, IScopedService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SearchConsoleApiClient> _logger;

    public SearchConsoleApiClient(IHttpClientFactory httpClientFactory, ILogger<SearchConsoleApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IList<SearchConsoleProperty>> ListPropertiesAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var http = CreateClient(accessToken);
        var response = await http.GetAsync("https://www.googleapis.com/webmasters/v3/sites", cancellationToken);
        if (!response.IsSuccessStatusCode) return [];

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!json.TryGetProperty("siteEntry", out var entries)) return [];

        var list = new List<SearchConsoleProperty>();
        foreach (var entry in entries.EnumerateArray())
        {
            var url = entry.GetProperty("siteUrl").GetString() ?? "";
            var perm = entry.TryGetProperty("permissionLevel", out var p) ? p.GetString() ?? "" : "";
            list.Add(new SearchConsoleProperty(url, perm));
        }
        return list;
    }

    public string? FindMatchingProperty(IList<SearchConsoleProperty> properties, string normalizedAuditUrl)
    {
        var uri = new Uri(normalizedAuditUrl);
        var candidates = new[]
        {
            $"{uri.Scheme}://{uri.Host}/",
            $"sc-domain:{uri.Host}",
            normalizedAuditUrl + "/",
            normalizedAuditUrl,
        };

        foreach (var c in candidates)
        {
            var match = properties.FirstOrDefault(p =>
                string.Equals(p.SiteUrl, c, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.SiteUrl;
        }

        return properties.FirstOrDefault(p =>
            p.SiteUrl.Contains(uri.Host, StringComparison.OrdinalIgnoreCase))?.SiteUrl;
    }

    public async Task<SearchConsoleAuditData?> FetchAuditDataAsync(
        string accessToken,
        string propertyUrl,
        IList<string> urlsToInspect,
        CancellationToken cancellationToken = default)
    {
        var http = CreateClient(accessToken);
        var endDate = DateTime.UtcNow.Date.AddDays(-1);
        var startDate = endDate.AddDays(-27);

        var analyticsPayload = new
        {
            startDate = startDate.ToString("yyyy-MM-dd"),
            endDate = endDate.ToString("yyyy-MM-dd"),
            dimensions = new[] { "query" },
            rowLimit = 25,
        };

        var encodedProperty = Uri.EscapeDataString(propertyUrl);
        var analyticsUrl =
            $"https://www.googleapis.com/webmasters/v3/sites/{encodedProperty}/searchAnalytics/query";

        var topQueries = new List<SearchAnalyticsRow>();
        var totalClicks = 0;
        var totalImpressions = 0;

        try
        {
            var analyticsRes = await http.PostAsJsonAsync(analyticsUrl, analyticsPayload, cancellationToken);
            if (analyticsRes.IsSuccessStatusCode)
            {
                var analyticsJson = await analyticsRes.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                if (analyticsJson.TryGetProperty("rows", out var rows))
                {
                    foreach (var row in rows.EnumerateArray())
                    {
                        var clicks = row.TryGetProperty("clicks", out var c) ? (int)c.GetDouble() : 0;
                        var impressions = row.TryGetProperty("impressions", out var i) ? (int)i.GetDouble() : 0;
                        var ctr = row.TryGetProperty("ctr", out var ct) ? ct.GetDouble() : 0;
                        var position = row.TryGetProperty("position", out var p) ? p.GetDouble() : 0;
                        var query = row.TryGetProperty("keys", out var keys) && keys.GetArrayLength() > 0
                            ? keys[0].GetString()
                            : null;
                        topQueries.Add(new SearchAnalyticsRow(query, clicks, impressions, ctr, position));
                        totalClicks += clicks;
                        totalImpressions += impressions;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search Analytics query failed for {Property}", propertyUrl);
        }

        var inspections = new List<UrlInspectionResult>();
        foreach (var url in urlsToInspect.Take(10))
        {
            try
            {
                var inspectPayload = new
                {
                    inspectionUrl = url,
                    siteUrl = propertyUrl,
                };
                var inspectRes = await http.PostAsJsonAsync(
                    "https://searchconsole.googleapis.com/v1/urlInspection/index:inspect",
                    inspectPayload,
                    cancellationToken);

                if (!inspectRes.IsSuccessStatusCode) continue;

                var inspectJson = await inspectRes.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                if (!inspectJson.TryGetProperty("inspectionResult", out var result)) continue;

                var indexResult = result.TryGetProperty("indexStatusResult", out var idx) ? idx : default;
                var verdict = indexResult.ValueKind != JsonValueKind.Undefined && indexResult.TryGetProperty("verdict", out var v)
                    ? v.GetString() ?? "UNKNOWN" : "UNKNOWN";
                var coverage = indexResult.ValueKind != JsonValueKind.Undefined && indexResult.TryGetProperty("coverageState", out var cs)
                    ? cs.GetString() ?? "" : "";
                var indexingState = indexResult.ValueKind != JsonValueKind.Undefined && indexResult.TryGetProperty("indexingState", out var is_)
                    ? is_.GetString() : null;

                string? richVerdict = null;
                if (result.TryGetProperty("richResultsResult", out var rich) && rich.TryGetProperty("verdict", out var rv))
                    richVerdict = rv.GetString();

                inspections.Add(new UrlInspectionResult(url, verdict, coverage, richVerdict, indexingState));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "URL Inspection failed for {Url}", url);
            }
        }

        return new SearchConsoleAuditData(propertyUrl, topQueries, inspections, totalClicks, totalImpressions);
    }

    public async Task<IList<SearchConsoleSitemapInfo>> ListSitemapsAsync(
        string accessToken,
        string propertyUrl,
        CancellationToken cancellationToken = default)
    {
        var http = CreateClient(accessToken);
        var encodedProperty = Uri.EscapeDataString(propertyUrl);
        var url = $"https://www.googleapis.com/webmasters/v3/sites/{encodedProperty}/sitemaps";

        try
        {
            var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!json.TryGetProperty("sitemap", out var entries)) return [];

            var list = new List<SearchConsoleSitemapInfo>();
            foreach (var entry in entries.EnumerateArray())
            {
                var path = entry.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                var errors = entry.TryGetProperty("errors", out var e) ? (int)e.GetDouble() : 0;
                var warnings = entry.TryGetProperty("warnings", out var w) ? (int)w.GetDouble() : 0;
                var pending = entry.TryGetProperty("isPending", out var ip) && ip.GetBoolean();
                var lastDownloaded = entry.TryGetProperty("lastDownloaded", out var ld) ? ld.GetString() : null;
                list.Add(new SearchConsoleSitemapInfo(path, errors, warnings, pending, lastDownloaded));
            }
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sitemap list failed for {Property}", propertyUrl);
            return [];
        }
    }

    private HttpClient CreateClient(string accessToken)
    {
        var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return http;
    }
}
