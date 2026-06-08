using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IContentQualityService
{
    Task AnalyzeTopPagesAsync(AuditRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// E-E-A-T / people-first content analysis via OpenAI-compatible API.
/// Skips gracefully when Llm:ApiKey is not configured.
/// </summary>
public partial class ContentQualityService : IContentQualityService, IScopedService
{
    private readonly IRepository<ScannedPage> _pageRepo;
    private readonly IRepository<ContentQualityScore> _scoreRepo;
    private readonly IAuditIssueWriter _issueWriter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IIntegrationSettingsService _integrationSettings;
    private readonly ILogger<ContentQualityService> _logger;

    public ContentQualityService(
        IRepository<ScannedPage> pageRepo,
        IRepository<ContentQualityScore> scoreRepo,
        IAuditIssueWriter issueWriter,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IIntegrationSettingsService integrationSettings,
        ILogger<ContentQualityService> logger)
    {
        _pageRepo = pageRepo;
        _scoreRepo = scoreRepo;
        _issueWriter = issueWriter;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _integrationSettings = integrationSettings;
        _logger = logger;
    }

    public async Task AnalyzeTopPagesAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        if (!_integrationSettings.IsEnabled("llm-eeat")) return;

        var apiKey = _config["Llm:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return;

        var model = _config["Llm:Model"] ?? "gpt-4o-mini";
        var maxPages = _config.GetValue("Llm:MaxPages", 5);

        var pages = await _pageRepo.Table
            .Where(p => p.AuditRunId == run.Id && p.Title != null)
            .OrderBy(p => p.CrawlDepth)
            .Take(maxPages)
            .ToListAsync(cancellationToken);

        if (pages.Count == 0) return;

        var http = _httpClientFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        foreach (var page in pages)
        {
            try
            {
                var prompt =
                    "Bu web sayfasını Google E-E-A-T ve insan odaklı içerik kalitesi açısından değerlendir.\n" +
                    $"URL: {page.Url}\n" +
                    $"Başlık: {page.Title}\n" +
                    $"Meta açıklama: {page.MetaDescription ?? "(yok)"}\n\n" +
                    "Yalnızca JSON döndür: eeatScore (0-100), checklist [{item, passed, note}], " +
                    "suggestions [string], issues [{ruleId, severity, message}]. " +
                    "Tüm metinleri Türkçe yaz. Google'ın insan odaklı içerik yönergelerini kullan.";

                var payload = new
                {
                    model,
                    messages = new[]
                    {
                        new { role = "system", content = "Sen bir SEO içerik kalitesi denetçisisin. Yalnızca geçerli JSON ile yanıt ver; tüm metinler Türkçe olsun." },
                        new { role = "user", content = prompt },
                    },
                    temperature = 0.2,
                    response_format = new { type = "json_object" },
                };

                var response = await http.PostAsJsonAsync(
                    "https://api.openai.com/v1/chat/completions",
                    payload,
                    cancellationToken);

                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                if (string.IsNullOrEmpty(content)) continue;

                using var result = JsonDocument.Parse(content);
                var root = result.RootElement;
                var score = root.TryGetProperty("eeatScore", out var s) ? s.GetInt32() : 50;
                var checklist = root.TryGetProperty("checklist", out var cl) ? cl.GetRawText() : "[]";
                var suggestions = root.TryGetProperty("suggestions", out var sg) ? sg.GetRawText() : null;

                await _scoreRepo.InsertAsync(new ContentQualityScore
                {
                    AuditRunId = run.Id,
                    Url = page.Url,
                    EeatScore = score,
                    ChecklistJson = checklist,
                    SuggestionsJson = suggestions,
                    CreatedAtUtc = DateTime.UtcNow,
                }, publishEvent: false);

                if (score < 50)
                {
                    await RecordContentIssueAsync(run, page.Url, "EEAT-001", AuditIssueSeverity.Warning,
                        $"Bu sayfa için E-E-A-T içerik kalitesi skoru {score}/100.",
                        "Uzmanlık sinyallerini güçlendirin, yazar bilgisi ekleyin ve özgün insan odaklı içerik sağlayın.",
                        cancellationToken);
                }

                if (root.TryGetProperty("issues", out var issues))
                {
                    foreach (var issue in issues.EnumerateArray())
                    {
                        var ruleId = issue.TryGetProperty("ruleId", out var r) ? r.GetString() ?? "EEAT-002" : "EEAT-002";
                        var sev = issue.TryGetProperty("severity", out var sv) && sv.GetString() == "warning"
                            ? AuditIssueSeverity.Warning : AuditIssueSeverity.Info;
                        var msg = issue.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(msg))
                            await RecordContentIssueAsync(run, page.Url, ruleId, sev, msg, null, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LLM content quality failed for {Url}", page.Url);
            }
        }
    }

    private async Task RecordContentIssueAsync(
        AuditRun run, string pageUrl, string ruleId, AuditIssueSeverity severity,
        string message, string? fixHint, CancellationToken cancellationToken)
    {
        await _issueWriter.RecordAsync(run, new AuditIssue
        {
            AuditRunId = run.Id,
            PageUrl = pageUrl,
            RuleId = ruleId,
            Category = "content-quality",
            Severity = severity,
            Source = AuditIssueSource.Llm,
            Message = message,
            FixHint = fixHint ?? "Google Search Central insan odaklı içerik yönergelerini uygulayın.",
            DocUrl = "https://developers.google.com/search/docs/fundamentals/creating-helpful-content",
            CreatedAt = DateTime.UtcNow,
        }, cancellationToken);
    }
}
