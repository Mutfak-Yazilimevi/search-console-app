using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Audit;

namespace SearchConsoleApp.Services.MerchantCenter;

public record GmcAiSummaryResult(string SummaryMarkdown, IList<string> Priorities);

public record GmcAiGenerateResult(string Content, string? JsonLd, string ContentType);

public record GmcAiExplainResult(string Explanation, IList<string> Steps);

public record GmcBulkAiItemResult(
    Guid ProductEntityId,
    string PageUrl,
    string? Title,
    GmcAiGenerateResult? Result,
    string? Error);

public interface IGeminiGmcComplianceService
{
    Task<GmcAiSummaryResult> GenerateActionSummaryAsync(Guid runEntityId, CancellationToken cancellationToken = default);
    Task<GmcAiGenerateResult> GenerateForProductAsync(
        Guid runEntityId,
        Guid productEntityId,
        string type,
        CancellationToken cancellationToken = default);
    Task<IList<GmcBulkAiItemResult>> BulkGenerateForTopProductsAsync(
        Guid runEntityId,
        string type,
        int maxProducts = 5,
        CancellationToken cancellationToken = default);
    Task<GmcAiGenerateResult> GenerateSiteContentAsync(
        Guid runEntityId,
        string type,
        CancellationToken cancellationToken = default);
    Task<GmcAiExplainResult> ExplainIssueAsync(
        Guid runEntityId,
        Guid issueEntityId,
        CancellationToken cancellationToken = default);
}

public partial class GeminiGmcComplianceService : IGeminiGmcComplianceService, IScopedService
{
    private readonly IRepository<ProductComplianceRun> _runRepo;
    private readonly IRepository<ProductComplianceItem> _itemRepo;
    private readonly IRepository<ProductComplianceIssue> _issueRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GeminiGmcComplianceService> _logger;

    public GeminiGmcComplianceService(
        IRepository<ProductComplianceRun> runRepo,
        IRepository<ProductComplianceItem> itemRepo,
        IRepository<ProductComplianceIssue> issueRepo,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<GeminiGmcComplianceService> logger)
    {
        _runRepo = runRepo;
        _itemRepo = itemRepo;
        _issueRepo = issueRepo;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GmcAiSummaryResult> GenerateActionSummaryAsync(
        Guid runEntityId,
        CancellationToken cancellationToken = default)
    {
        var run = await GetRunAsync(runEntityId, cancellationToken);
        var issues = await _issueRepo.Table.Where(i => i.RunId == run.Id).ToListAsync(cancellationToken);

        var grouped = issues
            .GroupBy(i => i.RuleId)
            .Select(g => $"{g.Key}: {g.Count()} adet — {g.First().Message}")
            .Take(15);

        var groupedText = string.Join("\n", grouped);
        var prompt =
            "Sen bir Google Merchant Center uzmanısın. Türkçe yanıt ver.\n" +
            $"Site: {run.NormalizedUrl}\n" +
            $"Uyumluluk skoru: {run.ComplianceScore}%\n" +
            $"Uyumlu: {run.CompliantCount}, Kısmen: {run.PartialCount}, Uyumsuz: {run.NonCompliantCount}\n" +
            "Sorun özeti:\n" + groupedText + "\n\n" +
            "5-7 maddelik öncelikli aksiyon planı yaz (markdown). GTIN/barkod uydurma.\n" +
            "JSON formatında yanıt ver: { \"summaryMarkdown\": \"...\", \"priorities\": [\"...\"] }";

        var json = await CallGeminiAsync(runEntityId, prompt, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var summary = root.TryGetProperty("summaryMarkdown", out var s) ? s.GetString() ?? "" : json;
        var priorities = new List<string>();
        if (root.TryGetProperty("priorities", out var p) && p.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in p.EnumerateArray())
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text)) priorities.Add(text);
            }
        }

        return new GmcAiSummaryResult(summary, priorities);
    }

    public async Task<GmcAiGenerateResult> GenerateForProductAsync(
        Guid runEntityId,
        Guid productEntityId,
        string type,
        CancellationToken cancellationToken = default)
    {
        var run = await GetRunAsync(runEntityId, cancellationToken);
        var item = await _itemRepo.GetByEntityIdAsync(productEntityId)
            ?? throw new AiGenerationException("Ürün bulunamadı.");
        if (item.RunId != run.Id) throw new AiGenerationException("Ürün bu analize ait değil.");

        var data = ExtractedProductData.FromJson(item.ExtractedDataJson) ?? new ExtractedProductData { Url = item.PageUrl };
        if (type.Equals("gtin", StringComparison.OrdinalIgnoreCase))
            throw new AiGenerationException("GTIN/barkod yapay zekâ ile üretilemez.");

        var prompt = type.ToLowerInvariant() switch
        {
            "title" => $"GMC uyumlu ürün başlığı (max 150 karakter, Türkçe). Marka: {data.Brand}. Ürün: {data.Name}. URL: {item.PageUrl}. JSON: {{\"content\":\"...\"}}",
            "description" => $"500+ karakter benzersiz Türkçe ürün açıklaması. Ürün: {data.Name}. Mevcut: {data.Description?.Substring(0, Math.Min(data.Description?.Length ?? 0, 200))}. JSON: {{\"content\":\"...\"}}",
            "schema" => $"Product+Offer JSON-LD. Fiyat: {data.SchemaPrice} {data.PriceCurrency}, availability: {data.Availability}, brand: {data.Brand}, name: {data.Name}, url: {item.PageUrl}. GTIN uydurma. JSON: {{\"jsonLd\":\"...\"}}",
            _ => throw new AiGenerationException($"Desteklenmeyen tip: {type}"),
        };

        var json = await CallGeminiAsync(runEntityId, prompt, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (type.Equals("schema", StringComparison.OrdinalIgnoreCase))
        {
            var ld = root.TryGetProperty("jsonLd", out var j) ? j.GetString() ?? "" : json;
            return new GmcAiGenerateResult(ld, ld, "application/ld+json");
        }

        var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? "" : json;
        return new GmcAiGenerateResult(content, null, "text/plain");
    }

    public async Task<IList<GmcBulkAiItemResult>> BulkGenerateForTopProductsAsync(
        Guid runEntityId,
        string type,
        int maxProducts = 5,
        CancellationToken cancellationToken = default)
    {
        if (type.Equals("gtin", StringComparison.OrdinalIgnoreCase))
            throw new AiGenerationException("GTIN/barkod yapay zekâ ile üretilemez.");

        var run = await GetRunAsync(runEntityId, cancellationToken);
        maxProducts = Math.Clamp(maxProducts, 1, 10);
        var items = await _itemRepo.Table
            .Where(i => i.RunId == run.Id)
            .OrderByDescending(i => i.IssueCount)
            .ThenBy(i => i.PageUrl)
            .Take(maxProducts)
            .ToListAsync(cancellationToken);

        var results = new List<GmcBulkAiItemResult>();
        foreach (var item in items)
        {
            try
            {
                var generated = await GenerateForProductAsync(runEntityId, item.EntityId, type, cancellationToken);
                results.Add(new GmcBulkAiItemResult(
                    item.EntityId, item.PageUrl, item.Title, generated, null));
            }
            catch (AiGenerationException ex)
            {
                results.Add(new GmcBulkAiItemResult(
                    item.EntityId, item.PageUrl, item.Title, null, ex.Message));
            }
        }

        return results;
    }

    public async Task<GmcAiGenerateResult> GenerateSiteContentAsync(
        Guid runEntityId,
        string type,
        CancellationToken cancellationToken = default)
    {
        var run = await GetRunAsync(runEntityId, cancellationToken);
        var prompt = type.ToLowerInvariant() switch
        {
            "return-policy" => $"Türkçe e-ticaret iade politikası HTML taslağı. Site: {run.NormalizedUrl}. JSON: {{\"content\":\"<section>...</section>\"}}",
            "shipping" => $"Türkçe kargo/teslimat bilgisi HTML taslağı. Site: {run.NormalizedUrl}. JSON: {{\"content\":\"<section>...</section>\"}}",
            _ => throw new AiGenerationException($"Desteklenmeyen tip: {type}"),
        };

        var json = await CallGeminiAsync(runEntityId, prompt, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.TryGetProperty("content", out var c) ? c.GetString() ?? "" : json;
        return new GmcAiGenerateResult(content, null, "text/html");
    }

    public async Task<GmcAiExplainResult> ExplainIssueAsync(
        Guid runEntityId,
        Guid issueEntityId,
        CancellationToken cancellationToken = default)
    {
        var run = await GetRunAsync(runEntityId, cancellationToken);
        var issue = await _issueRepo.GetByEntityIdAsync(issueEntityId)
            ?? throw new AiGenerationException("Sorun bulunamadı.");
        if (issue.RunId != run.Id) throw new AiGenerationException("Sorun bu analize ait değil.");

        var prompt =
            "Merchant Center sorunu açıkla (Türkçe). " +
            $"Kural: {issue.RuleId}, Alan: {issue.Field}. " +
            $"Mesaj: {issue.Message}. Mevcut öneri: {issue.FixHint}. " +
            "JSON: { \"explanation\": \"...\", \"steps\": [\"...\"] }. GTIN uydurma.";

        var json = await CallGeminiAsync(runEntityId, prompt, cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var explanation = root.TryGetProperty("explanation", out var e) ? e.GetString() ?? "" : json;
        var steps = new List<string>();
        if (root.TryGetProperty("steps", out var st) && st.ValueKind == JsonValueKind.Array)
        {
            foreach (var step in st.EnumerateArray())
            {
                var t = step.GetString();
                if (!string.IsNullOrWhiteSpace(t)) steps.Add(t);
            }
        }

        return new GmcAiExplainResult(explanation, steps);
    }

    private async Task<ProductComplianceRun> GetRunAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var run = await _runRepo.GetByEntityIdAsync(entityId);
        if (run == null) throw new AiGenerationException("Analiz kaydı bulunamadı.");
        if (run.Status != ProductComplianceRunStatus.Completed)
            throw new AiGenerationException("Analiz tamamlanmadan AI özelliği kullanılamaz.");
        return run;
    }

    private async Task<string> CallGeminiAsync(Guid runEntityId, string prompt, CancellationToken cancellationToken)
    {
        EnsureAiQuota(runEntityId);

        var apiKey = _config["Google:GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiGenerationException("Gemini API anahtarı yapılandırılmamış.");

        var model = _config["Google:GeminiModel"] ?? "gemini-2.5-flash";
        var http = _httpClientFactory.CreateClient();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json" },
        };

        var response = await http.PostAsJsonAsync(url, body, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini GMC failed: {Status}", (int)response.StatusCode);
            throw new AiGenerationException("Gemini API isteği başarısız oldu.");
        }

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.GetArrayLength() == 0)
            throw new AiGenerationException("Gemini yanıtında içerik bulunamadı.");

        var text = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
        text = Regex.Replace(text, @"^```json\s*|\s*```$", "", RegexOptions.Multiline).Trim();
        return text;
    }

    private void EnsureAiQuota(Guid runEntityId)
    {
        var max = _config.GetValue("ProductCompliance:MaxAiCallsPerRun", 20);
        var key = $"gmc-ai-quota:{runEntityId}";
        var count = _cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(24);
            return 0;
        });

        if (count >= max)
            throw new AiGenerationException($"Bu analiz için AI çağrı limitine ulaşıldı (en fazla {max}).");

        _cache.Set(key, count + 1, TimeSpan.FromHours(24));
    }
}
