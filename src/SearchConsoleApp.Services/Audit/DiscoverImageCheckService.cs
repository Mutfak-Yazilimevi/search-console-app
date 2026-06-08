using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IDiscoverImageCheckService
{
    Task CheckDiscoverImagesAsync(AuditRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// og:image gerçek boyut doğrulama (Discover ≥1200px).
/// </summary>
public partial class DiscoverImageCheckService : IDiscoverImageCheckService, IScopedService
{
    private static readonly Regex OgImageRegex = new(
        @"<meta[^>]+property=[""']og:image[""'][^>]+content=[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OgImageWidthRegex = new(
        @"<meta[^>]+property=[""']og:image:width[""'][^>]+content=[""'](\d+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IRepository<ScannedPage> _pageRepo;
    private readonly IAuditIssueWriter _issueWriter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DiscoverImageCheckService> _logger;

    public DiscoverImageCheckService(
        IRepository<ScannedPage> pageRepo,
        IAuditIssueWriter issueWriter,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<DiscoverImageCheckService> logger)
    {
        _pageRepo = pageRepo;
        _issueWriter = issueWriter;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task CheckDiscoverImagesAsync(AuditRun run, CancellationToken cancellationToken = default)
    {
        var maxPages = _config.GetValue("Audit:DiscoverImageMaxPages", 10);
        var urls = await _pageRepo.Table
            .Where(p => p.AuditRunId == run.Id)
            .OrderBy(p => p.CrawlDepth)
            .Select(p => p.Url)
            .Take(maxPages)
            .ToListAsync(cancellationToken);

        if (urls.Count == 0) urls.Add(run.NormalizedUrl);

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SearchConsoleApp-Audit/1.0");
        client.Timeout = TimeSpan.FromSeconds(15);

        foreach (var pageUrl in urls)
        {
            await CheckPageAsync(run, client, pageUrl, cancellationToken);
        }
    }

    private async Task CheckPageAsync(
        AuditRun run, HttpClient client, string pageUrl, CancellationToken cancellationToken)
    {
        try
        {
            var html = await client.GetStringAsync(pageUrl, cancellationToken);
            var ogImage = ExtractOgImage(html, pageUrl);
            if (string.IsNullOrWhiteSpace(ogImage)) return;

            var metaWidth = ExtractOgImageWidth(html);
            if (metaWidth >= 1200) return;

            var (width, _) = await ProbeImageDimensionsAsync(client, ogImage, cancellationToken);
            if (width == null || width >= 1200) return;

            await _issueWriter.RecordAsync(run, new AuditIssue
            {
                AuditRunId = run.Id,
                PageUrl = pageUrl,
                RuleId = "DISC-001",
                Category = "discover",
                Severity = AuditIssueSeverity.Warning,
                Source = AuditIssueSource.Crawl,
                Message = "og:image gerçek genişliği Discover için yetersiz (<1200px).",
                Evidence = $"{ogImage} → {width}px",
                FixHint = "En az 1200px genişliğinde görsel kullanın.",
                DocUrl = "https://developers.google.com/search/docs/appearance/google-discover",
                CreatedAt = DateTime.UtcNow,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Discover image check failed for {Url}", pageUrl);
        }
    }

    private static string? ExtractOgImage(string html, string pageUrl)
    {
        var m = OgImageRegex.Match(html);
        if (!m.Success)
        {
            m = Regex.Match(html,
                @"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']og:image[""']",
                RegexOptions.IgnoreCase);
        }
        if (!m.Success) return null;
        var url = m.Groups[1].Value.Trim();
        return Uri.TryCreate(url, UriKind.Absolute, out _) ? url : new Uri(new Uri(pageUrl), url).ToString();
    }

    private static int? ExtractOgImageWidth(string html)
    {
        var m = OgImageWidthRegex.Match(html);
        if (!m.Success)
        {
            m = Regex.Match(html,
                @"<meta[^>]+content=[""'](\d+)[""'][^>]+property=[""']og:image:width[""']",
                RegexOptions.IgnoreCase);
        }
        return m.Success && int.TryParse(m.Groups[1].Value, out var w) ? w : null;
    }

    private static async Task<(int? width, int? height)> ProbeImageDimensionsAsync(
        HttpClient client, string imageUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
        request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 65535);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) return (null, null);

        var bytes = new byte[65536];
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var read = await stream.ReadAsync(bytes.AsMemory(0, bytes.Length), cancellationToken);
        if (read < 24) return (null, null);

        return TryReadDimensions(bytes.AsSpan(0, read));
    }

    internal static (int? width, int? height) TryReadDimensions(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 24 && data[0] == 0x89 && data[1] == 0x50)
        {
            var w = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            var h = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            return (w, h);
        }

        for (var i = 0; i < data.Length - 9; i++)
        {
            if (data[i] != 0xFF) continue;
            var marker = data[i + 1];
            if (marker is not (0xC0 or 0xC1 or 0xC2)) continue;
            if (i + 9 >= data.Length) break;
            var h = (data[i + 5] << 8) | data[i + 6];
            var w = (data[i + 7] << 8) | data[i + 8];
            return (w, h);
        }

        return (null, null);
    }
}
