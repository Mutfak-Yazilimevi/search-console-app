using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Audit.SearchConsole;

namespace SearchConsoleApp.Services.Audit;

public interface IMigrationCheckService
{
    Task CheckMigrationAsync(AuditRun run, string? migrationSourceUrl, CancellationToken cancellationToken = default);
}

public partial class MigrationCheckService : IMigrationCheckService, IScopedService
{
    private readonly IAuditIssueWriter _issueWriter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MigrationCheckService> _logger;

    public MigrationCheckService(
        IAuditIssueWriter issueWriter,
        IHttpClientFactory httpClientFactory,
        ILogger<MigrationCheckService> logger)
    {
        _issueWriter = issueWriter;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task CheckMigrationAsync(
        AuditRun run, string? migrationSourceUrl, CancellationToken cancellationToken = default)
    {
        var targetHost = new Uri(run.NormalizedUrl).Host;

        if (!string.IsNullOrWhiteSpace(migrationSourceUrl))
            await CheckRedirectChainAsync(run, migrationSourceUrl.Trim(), targetHost, cancellationToken);

        await CheckDomainExpiryAsync(run, targetHost, cancellationToken);
    }

    private async Task CheckRedirectChainAsync(
        AuditRun run, string sourceUrl, string targetHost, CancellationToken cancellationToken)
    {
        var normalized = sourceUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? sourceUrl
            : $"https://{sourceUrl}";

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SearchConsoleApp-Audit/1.0");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, normalized);
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            var status = (int)response.StatusCode;
            var location = response.Headers.Location?.ToString();

            if (status is 301 or 308 && location != null)
            {
                var resolved = Uri.TryCreate(normalized, UriKind.Absolute, out var baseUri)
                    && Uri.TryCreate(baseUri, location, out var targetUri)
                    ? targetUri
                    : null;
                var locHost = resolved?.Host ?? targetHost;

                if (!string.Equals(locHost, targetHost, StringComparison.OrdinalIgnoreCase)
                    && !locHost.EndsWith("." + targetHost, StringComparison.OrdinalIgnoreCase))
                {
                    await _issueWriter.RecordAsync(run, new AuditIssue
                    {
                        AuditRunId = run.Id,
                        PageUrl = normalized,
                        RuleId = "MIGR-001",
                        Category = "migration",
                        Severity = AuditIssueSeverity.Critical,
                        Source = AuditIssueSource.System,
                        Message = $"Eski domain {status} yönlendirmesi yeni siteye gitmiyor: {location}",
                        FixHint = "301/308 yönlendirmelerini yeni domain'e güncelleyin.",
                        DocUrl = "https://developers.google.com/search/docs/crawling-indexing/301-redirects",
                        CreatedAt = DateTime.UtcNow,
                    }, cancellationToken);
                }
                return;
            }

            if (status is not (301 or 308))
            {
                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = normalized,
                    RuleId = "MIGR-001",
                    Category = "migration",
                    Severity = AuditIssueSeverity.Warning,
                    Source = AuditIssueSource.System,
                    Message = $"Eski domain kalıcı yönlendirme (301/308) döndürmüyor: HTTP {status}.",
                    FixHint = "Site taşıması sonrası eski URL'ler 301 ile yeni domain'e yönlendirilmeli.",
                    DocUrl = "https://developers.google.com/search/docs/crawling-indexing/301-redirects",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Migration redirect check failed for {Url}", normalized);
        }
    }

    private async Task CheckDomainExpiryAsync(AuditRun run, string host, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            var response = await client.GetAsync(
                $"https://rdap.org/domain/{Uri.EscapeDataString(host)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!json.TryGetProperty("events", out var events)) return;

            foreach (var ev in events.EnumerateArray())
            {
                if (!ev.TryGetProperty("eventAction", out var action) || action.GetString() != "expiration")
                    continue;

                if (!ev.TryGetProperty("eventDate", out var dateEl)) continue;
                if (!DateTime.TryParse(dateEl.GetString(), out var expiry)) continue;

                var daysLeft = (expiry.ToUniversalTime() - DateTime.UtcNow).TotalDays;
                if (daysLeft > 30) return;

                await _issueWriter.RecordAsync(run, new AuditIssue
                {
                    AuditRunId = run.Id,
                    PageUrl = run.NormalizedUrl,
                    RuleId = "MIGR-002",
                    Category = "migration",
                    Severity = daysLeft <= 7 ? AuditIssueSeverity.Critical : AuditIssueSeverity.Warning,
                    Source = AuditIssueSource.System,
                    Message = daysLeft <= 0
                        ? $"Domain süresi dolmuş veya bugün bitiyor ({host})."
                        : $"Domain süresi {Math.Ceiling(daysLeft)} gün içinde bitiyor ({expiry:yyyy-MM-dd}).",
                    FixHint = "Domain yenilemesini planlayın; süresi dolmuş domainler indeks kaybına yol açar.",
                    CreatedAt = DateTime.UtcNow,
                }, cancellationToken);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RDAP domain expiry check skipped for {Host}", host);
        }
    }
}
