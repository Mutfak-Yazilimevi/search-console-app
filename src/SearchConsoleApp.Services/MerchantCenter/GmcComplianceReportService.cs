using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.MerchantCenter;

public interface IGmcComplianceReportService
{
    Task<string?> BuildHtmlReportAsync(Guid runEntityId, CancellationToken cancellationToken = default);
}

public class GmcComplianceReportService : IGmcComplianceReportService, IScopedService
{
    private readonly IRepository<ProductComplianceRun> _runRepo;
    private readonly IRepository<ProductComplianceItem> _itemRepo;
    private readonly IRepository<ProductComplianceIssue> _issueRepo;
    private readonly IGmcComplianceDiffService _diffService;

    public GmcComplianceReportService(
        IRepository<ProductComplianceRun> runRepo,
        IRepository<ProductComplianceItem> itemRepo,
        IRepository<ProductComplianceIssue> issueRepo,
        IGmcComplianceDiffService diffService)
    {
        _runRepo = runRepo;
        _itemRepo = itemRepo;
        _issueRepo = issueRepo;
        _diffService = diffService;
    }

    public async Task<string?> BuildHtmlReportAsync(Guid runEntityId, CancellationToken cancellationToken = default)
    {
        var run = await _runRepo.GetByEntityIdAsync(runEntityId);
        if (run == null) return null;

        var products = await _itemRepo.Table
            .Where(i => i.RunId == run.Id)
            .OrderByDescending(i => i.IssueCount)
            .ThenBy(i => i.PageUrl)
            .ToListAsync(cancellationToken);

        var issues = await _issueRepo.Table
            .Where(i => i.RunId == run.Id)
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.RuleId)
            .ThenBy(i => i.PageUrl)
            .ToListAsync(cancellationToken);

        var priorities = GmcComplianceScoreCalculator.BuildPriorityActions(issues);
        var siteIssues = issues.Where(i =>
            i.Source is ProductComplianceIssueSource.SiteLevel
                or ProductComplianceIssueSource.SafeBrowsing).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"tr\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<title>Merchant Center Ürün Uyumluluk Raporu</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:system-ui,sans-serif;margin:2rem;color:#222;max-width:960px}");
        sb.AppendLine("h1,h2{margin-top:1.5rem} table{border-collapse:collapse;width:100%;font-size:0.9rem}");
        sb.AppendLine("th,td{border:1px solid #ddd;padding:0.4rem 0.6rem;text-align:left;vertical-align:top}");
        sb.AppendLine("th{background:#f5f5f5}.crit{color:#c0392b}.warn{color:#d68910}.info{color:#2980b9}");
        sb.AppendLine(".score{font-size:2rem;font-weight:700}.meta{color:#666;font-size:0.85rem}");
        sb.AppendLine(".fix{color:#555;font-size:0.85rem;margin-top:0.25rem}");
        sb.AppendLine("@media print{body{margin:1cm}a{color:inherit;text-decoration:none}}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine("<h1>Merchant Center Ürün Uyumluluk Raporu</h1>");
        sb.AppendLine($"<p class=\"meta\">{E(run.NormalizedUrl)} · {run.CompletedAt:yyyy-MM-dd HH:mm} UTC · {E(run.AnalysisMode.ToString())}</p>");
        sb.AppendLine("<p class=\"score\">");
        sb.AppendLine(run.ComplianceScore.HasValue ? $"{run.ComplianceScore}/100" : "—");
        sb.AppendLine("</p>");
        sb.AppendLine("<ul>");
        sb.AppendLine($"<li>Uyumlu ürün: <strong>{run.CompliantCount}</strong></li>");
        sb.AppendLine($"<li>Kısmi uyum: <strong>{run.PartialCount}</strong></li>");
        sb.AppendLine($"<li>Uyumsuz: <strong>{run.NonCompliantCount}</strong></li>");
        sb.AppendLine($"<li>Site hazırlık: <strong>{run.SiteReadinessScore ?? 0}/100</strong></li>");
        sb.AppendLine($"<li>Kritik: <strong>{run.CriticalCount}</strong> · Uyarı: <strong>{run.WarningCount}</strong> · Bilgi: <strong>{run.InfoCount}</strong></li>");
        sb.AppendLine("</ul>");

        var comparison = await _diffService.BuildComparisonAsync(run, cancellationToken);
        if (comparison != null)
        {
            sb.AppendLine("<h2>Önceki analizle karşılaştırma</h2>");
            sb.AppendLine($"<p class=\"meta\">Önceki skor: {comparison.PreviousComplianceScore ?? 0}% · Fark: {comparison.ComplianceScoreDelta:+0;-0;0}</p>");
            if (comparison.NewCriticalRuleIds.Count > 0)
                sb.AppendLine($"<p>Yeni kritik kurallar: {E(string.Join(", ", comparison.NewCriticalRuleIds))}</p>");
            if (comparison.ResolvedCriticalRuleIds.Count > 0)
                sb.AppendLine($"<p>Giderilen kritik kurallar: {E(string.Join(", ", comparison.ResolvedCriticalRuleIds))}</p>");
        }

        if (priorities.Count > 0)
        {
            sb.AppendLine("<h2>Öncelikli aksiyonlar</h2><ul>");
            foreach (var p in priorities)
            {
                sb.AppendLine($"<li><strong>{p.AffectedCount} ürün</strong> — {E(p.Message)}");
                if (!string.IsNullOrWhiteSpace(p.FixHint))
                    sb.AppendLine($"<div class=\"fix\">{E(p.FixHint)}</div>");
                sb.AppendLine("</li>");
            }
            sb.AppendLine("</ul>");
        }

        if (siteIssues.Count > 0)
        {
            sb.AppendLine("<h2>Site düzeyi sorunlar</h2>");
            AppendIssueTable(sb, siteIssues);
        }

        var crossIssues = issues.Where(i =>
            i.ItemId == null && i.Source == ProductComplianceIssueSource.CrossProduct).ToList();
        if (crossIssues.Count > 0)
        {
            sb.AppendLine("<h2>Çapraz ürün sorunları</h2>");
            AppendIssueTable(sb, crossIssues);
        }

        var feedIssues = issues.Where(i =>
            i.ItemId == null && i.Source == ProductComplianceIssueSource.MerchantCenter).ToList();
        if (feedIssues.Count > 0)
        {
            sb.AppendLine("<h2>Feed eşleştirme ve GMC</h2>");
            AppendIssueTable(sb, feedIssues);
        }

        if (products.Count > 0)
        {
            sb.AppendLine("<h2>Ürünler (sorun sayısına göre)</h2>");
            sb.AppendLine("<table><thead><tr><th>URL</th><th>Başlık</th><th>Skor</th><th>Durum</th><th>Sorun</th></tr></thead><tbody>");
            foreach (var product in products.Take(100))
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{E(product.PageUrl)}</td>");
                sb.AppendLine($"<td>{E(product.Title ?? "—")}</td>");
                sb.AppendLine($"<td>{product.ComplianceScore}</td>");
                sb.AppendLine($"<td>{E(product.Status.ToString())}</td>");
                sb.AppendLine($"<td>{product.IssueCount}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        sb.AppendLine("<h2>Tüm sorunlar</h2>");
        sb.AppendLine("<table><thead><tr><th>Önem</th><th>Kaynak</th><th>Kural</th><th>Sayfa</th><th>Mesaj</th></tr></thead><tbody>");
        foreach (var issue in issues.Take(200))
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td class=\"{SeverityClass(issue.Severity)}\">{E(issue.Severity.ToString())}</td>");
            sb.AppendLine($"<td>{E(issue.Source.ToString())}</td>");
            sb.AppendLine($"<td>{E(issue.RuleId)}</td>");
            sb.AppendLine($"<td>{E(issue.PageUrl ?? "Site geneli")}</td>");
            sb.AppendLine($"<td>{E(issue.Message)}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<p class=\"meta\">Google Merchant Center ürün spesifikasyonlarına göre otomatik üretilmiştir. Yazdır → PDF olarak kaydet.</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendIssueTable(StringBuilder sb, IList<ProductComplianceIssue> issueList)
    {
        sb.AppendLine("<table><thead><tr><th>Önem</th><th>Kural</th><th>Mesaj</th><th>Öneri</th></tr></thead><tbody>");
        foreach (var issue in issueList)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td class=\"{SeverityClass(issue.Severity)}\">{E(issue.Severity.ToString())}</td>");
            sb.AppendLine($"<td>{E(issue.RuleId)}</td>");
            sb.AppendLine($"<td>{E(issue.Message)}</td>");
            sb.AppendLine($"<td>{E(issue.FixHint)}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");
    }

    private static string SeverityClass(ProductComplianceIssueSeverity severity) => severity switch
    {
        ProductComplianceIssueSeverity.Critical => "crit",
        ProductComplianceIssueSeverity.Warning => "warn",
        _ => "info",
    };

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
}
