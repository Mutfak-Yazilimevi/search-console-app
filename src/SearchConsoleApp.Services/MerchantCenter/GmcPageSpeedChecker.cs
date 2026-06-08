using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.MerchantCenter;

public interface IGmcPageSpeedChecker
{
    Task CheckSampleProductsAsync(
        ProductComplianceRun run,
        IList<ProductComplianceItem> items,
        CancellationToken cancellationToken = default);
}

public class GmcPageSpeedChecker : IGmcPageSpeedChecker, IScopedService
{
    private readonly IRepository<ProductComplianceIssue> _issueRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<GmcPageSpeedChecker> _logger;

    public GmcPageSpeedChecker(
        IRepository<ProductComplianceIssue> issueRepo,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<GmcPageSpeedChecker> logger)
    {
        _issueRepo = issueRepo;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task CheckSampleProductsAsync(
        ProductComplianceRun run,
        IList<ProductComplianceItem> items,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _config["Google:PageSpeedApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || items.Count == 0) return;

        var maxProducts = _config.GetValue("ProductCompliance:PageSpeedMaxProducts", 5);
        var sample = items
            .OrderByDescending(i => i.IssueCount)
            .ThenBy(i => i.PageUrl)
            .Take(maxProducts)
            .ToList();

        var client = _httpClientFactory.CreateClient();
        var now = DateTime.UtcNow;

        foreach (var item in sample)
        {
            await RunForUrlAsync(run, item, client, apiKey, now, cancellationToken);
        }
    }

    private async Task RunForUrlAsync(
        ProductComplianceRun run,
        ProductComplianceItem item,
        HttpClient client,
        string apiKey,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var url = Uri.EscapeDataString(item.PageUrl);
        var psiUrl =
            $"https://www.googleapis.com/pagespeedonline/v5/runPagespeed?url={url}&strategy=mobile&category=performance&key={apiKey}";

        try
        {
            var response = await client.GetAsync(psiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("lighthouseResult", out var lighthouse)) return;
            if (!lighthouse.TryGetProperty("categories", out var categories)) return;
            if (!categories.TryGetProperty("performance", out var perf)) return;

            var score = perf.TryGetProperty("score", out var scoreEl) ? (int)(scoreEl.GetDouble() * 100) : 100;
            var audits = lighthouse.GetProperty("audits");
            var lcp = ReadMetric(audits, "largest-contentful-paint");
            var inp = ReadMetric(audits, "interaction-to-next-paint");
            var cls = ReadMetric(audits, "cumulative-layout-shift");

            await AddIssueIfAsync(run, item, now, ParseSeconds(lcp) > 4.0,
                "GMC-PERF-002", ProductComplianceIssueSeverity.Warning,
                $"LCP kötü: {lcp}.",
                "Ana içeriği hızlandırın; ürün görsellerini optimize edin.",
                lcp, cancellationToken);

            await AddIssueIfAsync(run, item, now, ParseSeconds(inp) > 0.5,
                "GMC-PERF-003", ProductComplianceIssueSeverity.Warning,
                $"INP kötü: {inp}.",
                "JavaScript yükünü azaltın; etkileşim gecikmesini düşürün.",
                inp, cancellationToken);

            await AddIssueIfAsync(run, item, now, ParseCls(cls) > 0.25,
                "GMC-PERF-004", ProductComplianceIssueSeverity.Warning,
                $"CLS kötü: {cls}.",
                "Görsel boyutlarını rezerve edin; layout kaymasını azaltın.",
                cls, cancellationToken);

            if (score < 50)
            {
                await _issueRepo.InsertAsync(new ProductComplianceIssue
                {
                    RunId = run.Id,
                    ItemId = item.Id,
                    PageUrl = item.PageUrl,
                    RuleId = "GMC-PERF-001",
                    Field = "page",
                    Severity = score < 30
                        ? ProductComplianceIssueSeverity.Critical
                        : ProductComplianceIssueSeverity.Warning,
                    Source = ProductComplianceIssueSource.PageSpeed,
                    Message = $"Mobil PageSpeed performans skoru {score}/100.",
                    FixHint = "Ürün sayfası hızını artırın — görselleri sıkıştırın, engelleyici kaynakları azaltın.",
                    DocUrl = "https://developers.google.com/search/docs/appearance/core-web-vitals",
                    Evidence = $"Skor: {score}/100 · LCP: {lcp} · INP: {inp} · CLS: {cls}",
                    CreatedAt = now,
                }, publishEvent: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PageSpeed check failed for product {Url}", item.PageUrl);
        }
    }

    private async Task AddIssueIfAsync(
        ProductComplianceRun run,
        ProductComplianceItem item,
        DateTime now,
        bool condition,
        string ruleId,
        ProductComplianceIssueSeverity severity,
        string message,
        string fixHint,
        string? evidence,
        CancellationToken cancellationToken)
    {
        if (!condition) return;

        await _issueRepo.InsertAsync(new ProductComplianceIssue
        {
            RunId = run.Id,
            ItemId = item.Id,
            PageUrl = item.PageUrl,
            RuleId = ruleId,
            Field = "page",
            Severity = severity,
            Source = ProductComplianceIssueSource.PageSpeed,
            Message = message,
            FixHint = fixHint,
            DocUrl = "https://developers.google.com/search/docs/appearance/core-web-vitals",
            Evidence = evidence,
            CreatedAt = now,
        }, publishEvent: false);
    }

    private static double ParseSeconds(string display)
    {
        if (string.IsNullOrEmpty(display) || display == "n/a") return 0;
        var num = new string(display.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(num, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static double ParseCls(string display)
    {
        if (string.IsNullOrEmpty(display) || display == "n/a") return 0;
        var num = new string(display.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return double.TryParse(num, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static string ReadMetric(JsonElement audits, string key)
    {
        if (!audits.TryGetProperty(key, out var audit)) return "n/a";
        if (audit.TryGetProperty("displayValue", out var display)) return display.GetString() ?? "n/a";
        return "n/a";
    }
}
