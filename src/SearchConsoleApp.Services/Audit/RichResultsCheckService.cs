using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IRichResultsCheckService
{
    Task CheckRichResultsAsync(AuditRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// Post-crawl JSON-LD doğrulama: FAQ, Product ve Article zengin sonuç alanları.
/// </summary>
public partial class RichResultsCheckService : IRichResultsCheckService, IScopedService
{
    private static readonly Regex JsonLdScriptRegex = new(
        @"<script[^>]+type=[""']application/ld\+json[""'][^>]*>([\s\S]*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IAuditIssueWriter _issueWriter;
    private readonly IRepository<ScannedPage> _pageRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<RichResultsCheckService> _logger;

    public RichResultsCheckService(
        IAuditIssueWriter issueWriter,
        IRepository<ScannedPage> pageRepo,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<RichResultsCheckService> logger)
    {
        _issueWriter = issueWriter;
        _pageRepo = pageRepo;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task CheckRichResultsAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        var maxPages = _config.GetValue("Audit:RichResultsMaxPages", 10);
        var urls = await _pageRepo.Table
            .Where(p => p.AuditRunId == run.Id)
            .OrderBy(p => p.CrawlDepth)
            .Select(p => p.Url)
            .Take(maxPages)
            .ToListAsync(cancellationToken);

        if (urls.Count == 0) return;

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SearchConsoleApp-Audit/1.0");

        foreach (var pageUrl in urls)
        {
            await ValidatePageAsync(run, client, pageUrl, cancellationToken);
        }
    }

    private async Task ValidatePageAsync(
        AuditRun run, HttpClient client, string pageUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(pageUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return;

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var blocks = ExtractJsonLdBlocks(html);
            if (blocks.Count == 0) return;

            var problems = new List<string>();
            foreach (var block in blocks)
            {
                try
                {
                    using var doc = JsonDocument.Parse(block);
                    CollectSchemaProblems(doc.RootElement, problems);
                }
                catch (JsonException)
                {
                    problems.Add("Geçersiz JSON-LD sözdizimi");
                    break;
                }
            }

            if (problems.Count == 0) return;

            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = pageUrl,
                RuleId = "RICH-002",
                Category = "structured-data",
                Severity = AuditIssueSeverity.Warning,
                Source = AuditIssueSource.Crawl,
                Message = "Zengin sonuç schema türünde eksik zorunlu alanlar var.",
                Evidence = IssueDetailEvidenceBuilder.Build(
                    "Zengin sonuç schema eksik alanlar",
                    problems.Distinct().Take(8).Select((p, i) => new IssueDetailItemDto
                    {
                        Label = $"Sorun #{i + 1}",
                        Value = p,
                    })),
                FixHint = "FAQPage, Product veya Article JSON-LD'de Google'ın zorunlu alanlarını ekleyin.",
                DocUrl = "https://developers.google.com/search/docs/appearance/structured-data",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rich results check failed for {Url}", pageUrl);
        }
    }

    private static List<string> ExtractJsonLdBlocks(string html)
    {
        var blocks = new List<string>();
        foreach (Match match in JsonLdScriptRegex.Matches(html))
        {
            var text = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(text)) blocks.Add(text);
        }
        return blocks;
    }

    private static void CollectSchemaProblems(JsonElement root, List<string> problems)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                CollectSchemaProblems(item, problems);
            return;
        }

        if (root.ValueKind != JsonValueKind.Object) return;

        if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in graph.EnumerateArray())
                ValidateTypedNode(node, problems);
        }

        ValidateTypedNode(root, problems);
    }

    private static void ValidateTypedNode(JsonElement node, List<string> problems)
    {
        if (node.ValueKind != JsonValueKind.Object) return;

        var type = GetTypeName(node);
        if (type == null) return;

        switch (type)
        {
            case "FAQPage":
                ValidateFaqPage(node, problems);
                break;
            case "Product":
                ValidateProduct(node, problems);
                break;
            case "Article":
            case "NewsArticle":
            case "BlogPosting":
                ValidateArticle(node, type, problems);
                break;
        }
    }

    private static void ValidateFaqPage(JsonElement node, List<string> problems)
    {
        if (!node.TryGetProperty("mainEntity", out var mainEntity)
            || mainEntity.ValueKind != JsonValueKind.Array
            || mainEntity.GetArrayLength() == 0)
        {
            problems.Add("FAQPage: mainEntity eksik veya boş");
            return;
        }

        var invalid = 0;
        foreach (var item in mainEntity.EnumerateArray())
        {
            var itemType = GetTypeName(item);
            if (itemType != "Question") { invalid++; continue; }
            if (!HasNonEmptyString(item, "name")) invalid++;
            if (!item.TryGetProperty("acceptedAnswer", out var answer)
                || answer.ValueKind != JsonValueKind.Object
                || !HasNonEmptyString(answer, "text"))
            {
                invalid++;
            }
        }

        if (invalid > 0)
            problems.Add($"FAQPage: {invalid} soru/cevap eksik alan içeriyor");
    }

    private static void ValidateProduct(JsonElement node, List<string> problems)
    {
        if (!HasNonEmptyString(node, "name"))
            problems.Add("Product: name eksik");

        var hasOffer = node.TryGetProperty("offers", out var offers)
            && offers.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        var hasRating = node.TryGetProperty("aggregateRating", out var rating)
            && rating.ValueKind == JsonValueKind.Object;
        var hasReview = node.TryGetProperty("review", out var review)
            && review.ValueKind is JsonValueKind.Object or JsonValueKind.Array;

        if (!hasOffer && !hasRating && !hasReview)
            problems.Add("Product: offers, aggregateRating veya review eksik");
    }

    private static void ValidateArticle(JsonElement node, string type, List<string> problems)
    {
        if (!HasNonEmptyString(node, "headline"))
            problems.Add($"{type}: headline eksik");
        if (!HasAuthor(node))
            problems.Add($"{type}: author eksik");
        if (!HasNonEmptyString(node, "datePublished"))
            problems.Add($"{type}: datePublished eksik");
    }

    private static bool HasAuthor(JsonElement node)
    {
        if (!node.TryGetProperty("author", out var author)) return false;
        return author.ValueKind switch
        {
            JsonValueKind.String => !string.IsNullOrWhiteSpace(author.GetString()),
            JsonValueKind.Object => HasNonEmptyString(author, "name"),
            JsonValueKind.Array => author.EnumerateArray().Any(a =>
                a.ValueKind == JsonValueKind.String
                    ? !string.IsNullOrWhiteSpace(a.GetString())
                    : HasNonEmptyString(a, "name")),
            _ => false,
        };
    }

    private static string? GetTypeName(JsonElement node)
    {
        if (!node.TryGetProperty("@type", out var typeEl)) return null;
        return typeEl.ValueKind switch
        {
            JsonValueKind.String => typeEl.GetString(),
            JsonValueKind.Array => typeEl.EnumerateArray()
                .FirstOrDefault(t => t.ValueKind == JsonValueKind.String)
                .GetString(),
            _ => null,
        };
    }

    private static bool HasNonEmptyString(JsonElement node, string property)
    {
        return node.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString());
    }
}
