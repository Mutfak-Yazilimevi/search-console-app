using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IAuditReportService
{
    Task<string?> BuildHtmlReportAsync(Guid auditRunEntityId, CancellationToken cancellationToken = default);
    Task<string?> BuildCriticalHtmlReportAsync(Guid auditRunEntityId, CancellationToken cancellationToken = default);
}

public partial class AuditReportService : IAuditReportService, IScopedService
{
    private readonly IRepository<AuditRun> _auditRunRepository;
    private readonly IRepository<AuditIssue> _issueRepository;
    private readonly IRepository<ScannedPage> _pageRepository;

    public AuditReportService(
        IRepository<AuditRun> auditRunRepository,
        IRepository<AuditIssue> issueRepository,
        IRepository<ScannedPage> pageRepository)
    {
        _auditRunRepository = auditRunRepository;
        _issueRepository = issueRepository;
        _pageRepository = pageRepository;
    }

    public Task<string?> BuildHtmlReportAsync(
        Guid auditRunEntityId, CancellationToken cancellationToken = default)
        => BuildReportAsync(auditRunEntityId, criticalOnly: false, cancellationToken);

    public Task<string?> BuildCriticalHtmlReportAsync(
        Guid auditRunEntityId, CancellationToken cancellationToken = default)
        => BuildReportAsync(auditRunEntityId, criticalOnly: true, cancellationToken);

    private async Task<string?> BuildReportAsync(
        Guid auditRunEntityId, bool criticalOnly, CancellationToken cancellationToken)
    {
        var run = await _auditRunRepository.GetByEntityIdAsync(auditRunEntityId);
        if (run == null) return null;

        var issuesQuery = _issueRepository.Table.Where(i => i.AuditRunId == run.Id);
        if (criticalOnly)
            issuesQuery = issuesQuery.Where(i => i.Severity == AuditIssueSeverity.Critical);

        var issues = await issuesQuery
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.RuleId)
            .ThenBy(i => i.PageUrl)
            .ToListAsync(cancellationToken);

        var newCriticalRules = ExtractNewCriticalRuleIds(
            await _issueRepository.Table
                .Where(i => i.AuditRunId == run.Id && i.RuleId == "SCHED-002")
                .ToListAsync(cancellationToken));

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"tr\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine(criticalOnly
            ? "<title>Kritik SEO Sorunları Raporu</title>"
            : "<title>SEO Denetim Raporu</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:system-ui,sans-serif;margin:2rem;color:#222;max-width:960px}");
        sb.AppendLine("h1,h2{margin-top:1.5rem} table{border-collapse:collapse;width:100%;font-size:0.9rem}");
        sb.AppendLine("th,td{border:1px solid #ddd;padding:0.4rem 0.6rem;text-align:left;vertical-align:top}");
        sb.AppendLine("th{background:#f5f5f5}.crit{color:#c0392b}.warn{color:#d68910}.info{color:#2980b9}");
        sb.AppendLine(".score{font-size:2rem;font-weight:700}.meta{color:#666;font-size:0.85rem}");
        sb.AppendLine(".card{border:1px solid #f0d0d0;border-radius:8px;padding:1rem;margin:1rem 0;background:#fff5f5}");
        sb.AppendLine(".new{display:inline-block;background:#c0392b;color:#fff;font-size:0.75rem;padding:0.1rem 0.4rem;border-radius:4px;margin-right:0.35rem}");
        sb.AppendLine("ul.pages{margin:0.5rem 0 0 1.2rem;font-size:0.85rem}");
        sb.AppendLine("@media print{body{margin:1cm}a{color:inherit;text-decoration:none}}");
        sb.AppendLine("</style></head><body>");

        if (criticalOnly)
        {
            sb.AppendLine("<h1>Kritik Sorunlar Raporu</h1>");
            sb.AppendLine($"<p class=\"meta\">{E(run.NormalizedUrl)} · {run.CompletedAt:yyyy-MM-dd HH:mm} UTC</p>");
            sb.AppendLine("<ul class=\"meta\">");
            sb.AppendLine($"<li>SEO skoru: <strong>{(run.Score.HasValue ? $"{run.Score}/100" : "—")}</strong></li>");
            sb.AppendLine($"<li>Kritik kayıt: <strong>{run.CriticalCount}</strong></li>");
            sb.AppendLine($"<li>Farklı kritik kural: <strong>{issues.GroupBy(i => i.RuleId).Count()}</strong></li>");
            if (newCriticalRules.Count > 0)
                sb.AppendLine($"<li>Önceki taramaya göre yeni: <strong>{string.Join(", ", newCriticalRules)}</strong></li>");
            sb.AppendLine("</ul>");

            if (issues.Count == 0)
            {
                sb.AppendLine("<p>Bu taramada kritik düzeyinde sorun tespit edilmedi.</p>");
            }
            else
            {
                foreach (var group in issues.GroupBy(i => i.RuleId).OrderByDescending(g => g.Count()))
                {
                    var sample = group.First();
                    var isNew = newCriticalRules.Contains(group.Key);
                    sb.AppendLine("<div class=\"card\">");
                    sb.AppendLine("<h2>");
                    if (isNew) sb.AppendLine("<span class=\"new\">YENİ</span>");
                    sb.AppendLine($"{E(group.Key)} — {E(sample.Message)}</h2>");
                    if (!string.IsNullOrWhiteSpace(sample.FixHint))
                        sb.AppendLine($"<p><strong>Öneri:</strong> {E(sample.FixHint)}</p>");
                    sb.AppendLine($"<p class=\"meta\">{group.Count()} etkilenen sayfa · {E(sample.Category)}</p>");
                    sb.AppendLine("<ul class=\"pages\">");
                    foreach (var page in group.Select(i => i.PageUrl).Distinct().Take(30))
                        sb.AppendLine($"<li>{E(string.IsNullOrWhiteSpace(page) ? "Site geneli" : page)}</li>");
                    if (group.Select(i => i.PageUrl).Distinct().Count() > 30)
                        sb.AppendLine("<li>…</li>");
                    sb.AppendLine("</ul>");
                    if (!string.IsNullOrWhiteSpace(sample.DocUrl))
                        sb.AppendLine($"<p><a href=\"{E(sample.DocUrl)}\">Google dokümantasyonu</a></p>");
                    sb.AppendLine("</div>");
                }
            }
        }
        else
        {
            var pages = await _pageRepository.Table
                .Where(p => p.AuditRunId == run.Id)
                .OrderBy(p => p.CrawlDepth)
                .Take(50)
                .ToListAsync(cancellationToken);

            sb.AppendLine("<h1>SEO Denetim Raporu</h1>");
            sb.AppendLine($"<p class=\"meta\">{E(run.NormalizedUrl)} · {run.CompletedAt:yyyy-MM-dd HH:mm} UTC · {run.Mode}</p>");
            sb.AppendLine("<p class=\"score\">");
            sb.AppendLine(run.Score.HasValue ? $"{run.Score}/100" : "—");
            sb.AppendLine("</p>");
            sb.AppendLine("<ul>");
            sb.AppendLine($"<li>Kritik: <strong>{run.CriticalCount}</strong></li>");
            sb.AppendLine($"<li>Uyarı: <strong>{run.WarningCount}</strong></li>");
            sb.AppendLine($"<li>Bilgi: <strong>{run.InfoCount}</strong></li>");
            sb.AppendLine($"<li>Taranan sayfa: <strong>{run.PagesCrawled}</strong></li>");
            sb.AppendLine("</ul>");

            if (run.CriticalCount > 0)
            {
                sb.AppendLine("<h2>Kritik sorunlar (özet)</h2>");
                foreach (var group in issues
                    .Where(i => i.Severity == AuditIssueSeverity.Critical)
                    .GroupBy(i => i.RuleId)
                    .OrderByDescending(g => g.Count())
                    .Take(20))
                {
                    var sample = group.First();
                    sb.AppendLine($"<p class=\"crit\"><strong>{E(group.Key)}</strong> — {E(sample.Message)} ({group.Count()} sayfa)</p>");
                }
            }

            sb.AppendLine("<h2>Tüm sorunlar</h2>");
            sb.AppendLine("<table><thead><tr><th>Önem</th><th>Kural</th><th>Sayfa</th><th>Mesaj</th></tr></thead><tbody>");
            foreach (var issue in issues.Take(100))
            {
                var cls = issue.Severity switch
                {
                    AuditIssueSeverity.Critical => "crit",
                    AuditIssueSeverity.Warning => "warn",
                    _ => "info",
                };
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td class=\"{cls}\">{E(issue.Severity.ToString())}</td>");
                sb.AppendLine($"<td>{E(issue.RuleId)}</td>");
                sb.AppendLine($"<td>{E(issue.PageUrl)}</td>");
                sb.AppendLine($"<td>{E(issue.Message)}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");

            if (pages.Count > 0)
            {
                sb.AppendLine("<h2>Taranan sayfalar (ilk 50)</h2>");
                sb.AppendLine("<table><thead><tr><th>URL</th><th>HTTP</th><th>Title</th><th>Derinlik</th></tr></thead><tbody>");
                foreach (var page in pages)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{E(page.Url)}</td>");
                    sb.AppendLine($"<td>{page.StatusCode}</td>");
                    sb.AppendLine($"<td>{E(page.Title ?? "—")}</td>");
                    sb.AppendLine($"<td>{page.CrawlDepth}</td>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</tbody></table>");
            }
        }

        sb.AppendLine("<p class=\"meta\">Google Search Central kurallarına göre otomatik üretilmiştir. Yazdır → PDF olarak kaydet.</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    internal static HashSet<string> ExtractNewCriticalRuleIds(IList<AuditIssue> schedIssues)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in schedIssues)
        {
            var match = Regex.Match(issue.Message ?? "", @":\s*([A-Za-z0-9_-]+)\.?$");
            if (match.Success) ids.Add(match.Groups[1].Value);
        }
        return ids;
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
}
