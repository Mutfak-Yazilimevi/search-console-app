using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public class AiGenerationException : Exception
{
    public AiGenerationException(string message) : base(message) { }
}

public record FaqItemDto(string Question, string Answer);

public record FaqGenerationResult(
    string PageUrl,
    IReadOnlyList<FaqItemDto> Questions,
    string HtmlSection,
    string JsonLd);

public record MetaGenerationResult(
    string PageUrl,
    string Title,
    string MetaDescription,
    string TitleTagHtml,
    string MetaTagHtml);

public record AltTextSuggestionDto(string Src, string AltText, string ImgHtmlSnippet);

public record AltTextGenerationResult(
    string PageUrl,
    IReadOnlyList<AltTextSuggestionDto> Images);

public interface IGeminiFaqGenerationService
{
    Task<FaqGenerationResult> GenerateForPageAsync(
        Guid auditRunEntityId,
        string pageUrl,
        CancellationToken cancellationToken = default);

    Task<MetaGenerationResult> GenerateMetaAsync(
        Guid auditRunEntityId,
        string pageUrl,
        string target = "seo",
        CancellationToken cancellationToken = default);

    Task<AltTextGenerationResult> GenerateAltTextAsync(
        Guid auditRunEntityId,
        string pageUrl,
        IReadOnlyList<string>? imageSrcs = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Gemini API ile sayfa içeriğine uygun SSS + FAQPage JSON-LD üretir.
/// </summary>
public partial class GeminiFaqGenerationService : IGeminiFaqGenerationService, IScopedService
{
    private readonly IRepository<AuditRun> _auditRunRepository;
    private readonly IRepository<ScannedPage> _pageRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IIntegrationSettingsService _integrationSettings;
    private readonly ILogger<GeminiFaqGenerationService> _logger;

    public GeminiFaqGenerationService(
        IRepository<AuditRun> auditRunRepository,
        IRepository<ScannedPage> pageRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IIntegrationSettingsService integrationSettings,
        ILogger<GeminiFaqGenerationService> logger)
    {
        _auditRunRepository = auditRunRepository;
        _pageRepository = pageRepository;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _integrationSettings = integrationSettings;
        _logger = logger;
    }

    public async Task<FaqGenerationResult> GenerateForPageAsync(
        Guid auditRunEntityId,
        string pageUrl,
        CancellationToken cancellationToken = default)
    {
        var (match, apiKey) = await ResolvePageAsync(auditRunEntityId, pageUrl, cancellationToken);

        var bodyExcerpt = await FetchBodyExcerptAsync(match.Url, cancellationToken);
        var prompt =
            "Sen bir SEO içerik uzmanısın. Aşağıdaki web sayfası için Google AI Özetleri ve arama snippet'lerine uygun, " +
            "gerçek kullanıcı niyetini yansıtan 4 veya 5 adet SSS (sık sorulan soru) üret.\n\n" +
            "Kurallar:\n" +
            "- Sorular kısa ve doğal Türkçe olsun.\n" +
            "- Cevaplar 1–3 cümle, net ve doğru bilgi içersin; uydurma fiyat/tarih/istatistik ekleme.\n" +
            "- Sayfa konusuyla doğrudan ilgili olsun.\n" +
            "- Yalnızca geçerli JSON döndür, markdown veya açıklama ekleme.\n\n" +
            "JSON şeması: { \"questions\": [ { \"question\": \"...\", \"answer\": \"...\" } ] }\n\n" +
            BuildPageContext(match.Url, match.Title, match.MetaDescription, bodyExcerpt);

        var text = await CallGeminiJsonAsync(apiKey, prompt, cancellationToken);
        var questions = ParseQuestions(text);

        if (questions.Count == 0)
            throw new AiGenerationException("Gemini geçerli soru-cevap üretemedi. Tekrar deneyin.");

        return new FaqGenerationResult(match.Url, questions, BuildHtmlSection(questions), BuildJsonLd(questions));
    }

    public async Task<MetaGenerationResult> GenerateMetaAsync(
        Guid auditRunEntityId,
        string pageUrl,
        string target = "seo",
        CancellationToken cancellationToken = default)
    {
        var (match, apiKey) = await ResolvePageAsync(auditRunEntityId, pageUrl, cancellationToken);
        var bodyExcerpt = await FetchBodyExcerptAsync(match.Url, cancellationToken);
        var isOpenGraph = string.Equals(target, "openGraph", StringComparison.OrdinalIgnoreCase);

        var prompt = isOpenGraph
            ? "Sen bir SEO uzmanısın. Aşağıdaki sayfa için sosyal paylaşım (Open Graph) başlığı ve açıklaması öner.\n\n" +
              "Kurallar:\n" +
              "- title: 40–70 karakter, dikkat çekici ve sayfa konusunu yansıtsın.\n" +
              "- metaDescription: 100–200 karakter, paylaşımda okunabilir özet olsun.\n" +
              "- Türkçe, doğal ve sayfa içeriğiyle uyumlu olsun.\n" +
              "- Yalnızca geçerli JSON döndür.\n\n" +
              "JSON şeması: { \"title\": \"...\", \"metaDescription\": \"...\" }\n\n" +
              BuildPageContext(match.Url, match.Title, match.MetaDescription, bodyExcerpt)
            : "Sen bir SEO uzmanısın. Aşağıdaki sayfa için arama sonuçlarında tıklanabilir bir title ve meta description öner.\n\n" +
              "Kurallar:\n" +
              "- title: 45–60 karakter, ana konuyu net yansıtsın, marka/site adı uygunsa sonda olsun.\n" +
              "- metaDescription: 120–155 karakter, fayda odaklı, CTA içerebilir, anahtar kelime doldurma yapma.\n" +
              "- Türkçe, doğal ve sayfa içeriğiyle uyumlu olsun.\n" +
              "- Yalnızca geçerli JSON döndür.\n\n" +
              "JSON şeması: { \"title\": \"...\", \"metaDescription\": \"...\" }\n\n" +
              BuildPageContext(match.Url, match.Title, match.MetaDescription, bodyExcerpt);

        var text = await CallGeminiJsonAsync(apiKey, prompt, cancellationToken);
        using var doc = JsonDocument.Parse(ExtractJson(text));
        var root = doc.RootElement;
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString()?.Trim() : null;
        var desc = root.TryGetProperty("metaDescription", out var dEl) ? dEl.GetString()?.Trim() : null;

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(desc))
            throw new AiGenerationException("Gemini geçerli meta önerisi üretemedi. Tekrar deneyin.");

        return new MetaGenerationResult(
            match.Url,
            title,
            desc,
            isOpenGraph
                ? $"<meta property=\"og:title\" content=\"{EscapeHtml(title)}\" />"
                : $"<title>{EscapeHtml(title)}</title>",
            isOpenGraph
                ? $"<meta property=\"og:description\" content=\"{EscapeHtml(desc)}\" />"
                : $"<meta name=\"description\" content=\"{EscapeHtml(desc)}\" />");
    }

    public async Task<AltTextGenerationResult> GenerateAltTextAsync(
        Guid auditRunEntityId,
        string pageUrl,
        IReadOnlyList<string>? imageSrcs = null,
        CancellationToken cancellationToken = default)
    {
        var (match, apiKey) = await ResolvePageAsync(auditRunEntityId, pageUrl, cancellationToken);
        var bodyExcerpt = await FetchBodyExcerptAsync(match.Url, cancellationToken);

        var srcList = (imageSrcs ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        if (srcList.Count == 0)
        {
            var html = await FetchHtmlAsync(match.Url, cancellationToken);
            srcList = ExtractMissingAltSrcs(html).Take(12).ToList();
        }

        if (srcList.Count == 0)
            throw new AiGenerationException("Alt metni eksik görsel bulunamadı.");

        var srcLines = string.Join("\n", srcList.Select((s, i) => $"{i + 1}. {s}"));
        var prompt =
            "Sen bir erişilebilirlik ve SEO uzmanısın. Aşağıdaki görseller için kısa, açıklayıcı alt metinler öner.\n\n" +
            "Kurallar:\n" +
            "- Her alt metin 5–12 kelime, Türkçe, görseli tarif etsin.\n" +
            "- Anahtar kelime doldurma yapma; dekoratif görseller için alt boş bırakma, kısa tarif yaz.\n" +
            "- Yalnızca geçerli JSON döndür.\n\n" +
            "JSON şeması: { \"images\": [ { \"src\": \"...\", \"alt\": \"...\" } ] }\n\n" +
            BuildPageContext(match.Url, match.Title, match.MetaDescription, bodyExcerpt) +
            "\n\nAlt metni eksik görseller:\n" + srcLines;

        var text = await CallGeminiJsonAsync(apiKey, prompt, cancellationToken);
        using var doc = JsonDocument.Parse(ExtractJson(text));
        if (!doc.RootElement.TryGetProperty("images", out var arr) || arr.ValueKind != JsonValueKind.Array)
            throw new AiGenerationException("Gemini geçerli alt metin önerisi üretemedi.");

        var results = new List<AltTextSuggestionDto>();
        foreach (var item in arr.EnumerateArray())
        {
            var src = item.TryGetProperty("src", out var sEl) ? sEl.GetString()?.Trim() : null;
            var alt = item.TryGetProperty("alt", out var aEl) ? aEl.GetString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(alt)) continue;
            results.Add(new AltTextSuggestionDto(
                src,
                alt,
                $"<img src=\"{EscapeHtml(src)}\" alt=\"{EscapeHtml(alt)}\" />"));
        }

        if (results.Count == 0)
            throw new AiGenerationException("Gemini geçerli alt metin önerisi üretemedi.");

        return new AltTextGenerationResult(match.Url, results);
    }

    private async Task<(ScannedPage Match, string ApiKey)> ResolvePageAsync(
        Guid auditRunEntityId,
        string pageUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pageUrl))
            throw new ArgumentException("pageUrl is required.", nameof(pageUrl));

        if (!_integrationSettings.IsEnabled("gemini"))
            throw new AiGenerationException("Gemini entegrasyonu devre dışı. Sistem entegrasyonları panelinden etkinleştirin.");

        var apiKey = _config["Google:GeminiApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AiGenerationException("Gemini API anahtarı yapılandırılmamış. Google:GeminiApiKey ekleyin.");

        var run = await _auditRunRepository.GetByEntityIdAsync(auditRunEntityId);
        if (run == null)
            throw new AiGenerationException("Denetim kaydı bulunamadı.");

        var normalizedTarget = NormalizeUrl(pageUrl);
        var pages = await _pageRepository.Table
            .Where(p => p.AuditRunId == run.Id)
            .ToListAsync(cancellationToken);

        var match = pages.FirstOrDefault(p => NormalizeUrl(p.Url) == normalizedTarget);
        if (match == null)
            throw new AiGenerationException("Bu URL bu taramaya ait değil.");

        return (match, apiKey);
    }

    private static string BuildPageContext(string url, string? title, string? metaDescription, string bodyExcerpt) =>
        $"Sayfa URL: {url}\n" +
        $"Başlık: {title ?? "(yok)"}\n" +
        $"Meta açıklama: {metaDescription ?? "(yok)"}\n" +
        "İçerik özeti:\n" +
        (string.IsNullOrWhiteSpace(bodyExcerpt)
            ? "(içerik alınamadı — başlık ve meta açıklamaya göre üret)"
            : bodyExcerpt);

    private async Task<string> CallGeminiJsonAsync(
        string apiKey,
        string prompt,
        CancellationToken cancellationToken)
    {
        var model = _config["Google:GeminiModel"] ?? "gemini-flash-latest";
        var endpoint =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.4, responseMimeType = "application/json" },
        };

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(60);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("X-goog-api-key", apiKey);

        var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini request failed: {Status} {Body}", (int)response.StatusCode, responseBody);
            var detail = TryExtractGeminiError(responseBody);
            throw new AiGenerationException(
                string.IsNullOrWhiteSpace(detail)
                    ? "Gemini API isteği başarısız oldu. Anahtar ve model adını kontrol edin."
                    : $"Gemini API hatası: {detail}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.GetArrayLength() == 0)
            throw new AiGenerationException("Gemini yanıtında içerik bulunamadı.");

        if (candidates[0].TryGetProperty("content", out var contentEl)
            && contentEl.TryGetProperty("parts", out var partsEl))
        {
            foreach (var part in partsEl.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textEl))
                {
                    var text = textEl.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }
        }

        throw new AiGenerationException("Gemini boş yanıt döndürdü.");
    }

    private static string ExtractJson(string raw)
    {
        var json = raw.Trim();
        var fence = Regex.Match(json, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
        return fence.Success ? fence.Groups[1].Value.Trim() : json;
    }

    private async Task<string> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SearchConsoleApp-SEO-Audit/1.0");
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return "";
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not fetch HTML: {Url}", url);
            return "";
        }
    }

    private static List<string> ExtractMissingAltSrcs(string html)
    {
        var results = new List<string>();
        var regex = new Regex(@"<img\b[^>]*>", RegexOptions.IgnoreCase);
        foreach (Match match in regex.Matches(html))
        {
            var tag = match.Value;
            var altMatch = Regex.Match(tag, @"\balt=[""']([^""']*)[""']", RegexOptions.IgnoreCase);
            var altValue = altMatch.Success ? altMatch.Groups[1].Value.Trim() : null;
            if (altValue is { Length: > 0 }) continue;

            var src = Regex.Match(tag, @"\bsrc=[""']([^""']+)[""']", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(src))
                src = Regex.Match(tag, @"\bdata-src=[""']([^""']+)[""']", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
            results.Add(string.IsNullOrWhiteSpace(src) ? "(src belirtilmemiş)" : src);
        }

        return results;
    }

    private static string? TryExtractGeminiError(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var msg))
            {
                return msg.GetString();
            }
        }
        catch
        {
            // ignore parse errors
        }

        return null;
    }

    private static IReadOnlyList<FaqItemDto> ParseQuestions(string raw)
    {
        using var doc = JsonDocument.Parse(ExtractJson(raw));
        if (!doc.RootElement.TryGetProperty("questions", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<FaqItemDto>();
        foreach (var item in arr.EnumerateArray())
        {
            var q = item.TryGetProperty("question", out var qEl) ? qEl.GetString()?.Trim() : null;
            var a = item.TryGetProperty("answer", out var aEl) ? aEl.GetString()?.Trim() : null;
            if (!string.IsNullOrWhiteSpace(q) && !string.IsNullOrWhiteSpace(a))
                list.Add(new FaqItemDto(q, a));
        }

        return list;
    }

    private async Task<string> FetchBodyExcerptAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SearchConsoleApp-SEO-Audit/1.0");

            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return "";

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractVisibleText(html, 3500);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not fetch page body for FAQ generation: {Url}", url);
            return "";
        }
    }

    private static string ExtractVisibleText(string html, int maxChars)
    {
        var noScript = Regex.Replace(html, @"<script[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
        noScript = Regex.Replace(noScript, @"<style[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
        noScript = Regex.Replace(noScript, @"<!--[\s\S]*?-->", " ");
        var text = Regex.Replace(noScript, @"<[^>]+>", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length <= maxChars) return text;
        return text[..maxChars];
    }

    private static string BuildHtmlSection(IReadOnlyList<FaqItemDto> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<section id=\"sss\" aria-label=\"Sık Sorulan Sorular\">");
        sb.AppendLine("  <h2>Sık Sorulan Sorular</h2>");
        foreach (var item in items)
        {
            sb.AppendLine($"  <h3>{EscapeHtml(item.Question)}</h3>");
            sb.AppendLine($"  <p>{EscapeHtml(item.Answer)}</p>");
        }
        sb.AppendLine("</section>");
        return sb.ToString().TrimEnd();
    }

    private static string BuildJsonLd(IReadOnlyList<FaqItemDto> items)
    {
        var mainEntity = items.Select(i => new Dictionary<string, object>
        {
            ["@type"] = "Question",
            ["name"] = i.Question,
            ["acceptedAnswer"] = new Dictionary<string, object>
            {
                ["@type"] = "Answer",
                ["text"] = i.Answer,
            },
        }).ToList();

        var schema = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = mainEntity,
        };

        return JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string EscapeHtml(string value) =>
        value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string NormalizeUrl(string url)
    {
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return trimmed.TrimEnd('/');
        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
    }
}
