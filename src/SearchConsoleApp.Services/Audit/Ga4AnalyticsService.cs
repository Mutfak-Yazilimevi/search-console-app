using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Audit.SearchConsole;

namespace SearchConsoleApp.Services.Audit;

public interface IGa4AnalyticsService
{
    Task CheckTrafficTrendAsync(
        AuditRun run, string? ga4PropertyId, CancellationToken cancellationToken = default);
}

/// <summary>
/// GA4 Data API ile oturum trendi; yapılandırılmamışsa Search Console tıklama verisine düşer.
/// </summary>
public partial class Ga4AnalyticsService : IGa4AnalyticsService, IScopedService
{
    private readonly IRepository<SearchConsoleSnapshot> _snapshotRepository;
    private readonly ISearchConsoleAuthService _authService;
    private readonly IAuditIssueWriter _issueWriter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Ga4AnalyticsService> _logger;

    public Ga4AnalyticsService(
        IRepository<SearchConsoleSnapshot> snapshotRepository,
        ISearchConsoleAuthService authService,
        IAuditIssueWriter issueWriter,
        IHttpClientFactory httpClientFactory,
        ILogger<Ga4AnalyticsService> logger)
    {
        _snapshotRepository = snapshotRepository;
        _authService = authService;
        _issueWriter = issueWriter;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task CheckTrafficTrendAsync(
        AuditRun run, string? ga4PropertyId, CancellationToken cancellationToken = default)
    {
        if (run.Mode == AuditMode.Connected && run.CustomerId.HasValue
            && !string.IsNullOrWhiteSpace(ga4PropertyId))
        {
            var token = await _authService.GetAccessTokenAsync(run.CustomerId.Value, cancellationToken);
            if (token != null && await TryGa4ReportAsync(run, ga4PropertyId, token, cancellationToken))
                return;
        }

        await FallbackScTrendAsync(run, cancellationToken);
    }

    private async Task<bool> TryGa4ReportAsync(
        AuditRun run, string propertyId, string accessToken, CancellationToken cancellationToken)
    {
        var property = propertyId.StartsWith("properties/", StringComparison.OrdinalIgnoreCase)
            ? propertyId
            : $"properties/{propertyId}";

        var payload = new
        {
            dateRanges = new[]
            {
                new { startDate = "14daysAgo", endDate = "8daysAgo", name = "previous" },
                new { startDate = "7daysAgo", endDate = "yesterday", name = "current" },
            },
            metrics = new[] { new { name = "sessions" } },
        };

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await client.PostAsJsonAsync(
                $"https://analyticsdata.googleapis.com/v1beta/{property}:runReport",
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!json.TryGetProperty("rows", out var rows) || rows.GetArrayLength() < 2) return false;

            var values = rows.EnumerateArray()
                .Select(r => r.GetProperty("metricValues")[0].GetProperty("value").GetInt32())
                .ToList();

            if (values.Count < 2) return false;

            var previous = values[0];
            var current = values[1];
            if (previous < 50) return true;

            if (current < previous * 0.7)
            {
                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = run.NormalizedUrl,
                    RuleId = "ANALYTICS-001",
                    Category = "analytics",
                    Severity = AuditIssueSeverity.Warning,
                    Source = AuditIssueSource.System,
                    Message = $"GA4 oturum düşüşü: son 7 gün {current} vs önceki 7 gün {previous} (%{((current - previous) * 100.0 / previous):F0}).",
                    FixHint = "Organik trafik, teknik indeks ve içerik değişikliklerini birlikte inceleyin.",
                    DocUrl = "https://developers.google.com/analytics/devguides/reporting/data/v1",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GA4 report failed for property {PropertyId}", propertyId);
            return false;
        }
    }

    private async Task FallbackScTrendAsync(AuditRun run, CancellationToken cancellationToken)
    {
        if (run.Mode != AuditMode.Connected) return;

        var current = await _snapshotRepository.Table
            .FirstOrDefaultAsync(s => s.AuditRunId == run.Id, cancellationToken);

        if (current == null || string.IsNullOrWhiteSpace(current.PerformanceJson)) return;

        try
        {
            using var doc = JsonDocument.Parse(current.PerformanceJson);
            var clicks = doc.RootElement.TryGetProperty("TotalClicks28d", out var c) ? c.GetInt32() : 0;
            if (clicks == 0 && run.PagesCrawled > 5)
            {
                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = run.NormalizedUrl,
                    RuleId = "ANALYTICS-001",
                    Category = "analytics",
                    Severity = AuditIssueSeverity.Info,
                    Source = AuditIssueSource.SearchConsole,
                    Message = "Son 28 günde organik tıklama yok (GA4 yapılandırılmamış, SC verisi).",
                    FixHint = "GA4 property ID ekleyerek daha ayrıntılı trafik trendi alın.",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SC fallback analytics check failed");
        }
    }
}
