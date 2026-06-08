using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Core.Realtime;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Email;
using SearchConsoleApp.Services.Outbox;

namespace SearchConsoleApp.Services.Audit;

public interface IAuditNotificationService
{
    Task NotifyCompletedAsync(AuditRun run, CancellationToken cancellationToken = default);
    Task NotifyFailedAsync(AuditRun run, CancellationToken cancellationToken = default);
}

public partial class AuditNotificationService : IAuditNotificationService, IScopedService
{
    private readonly IRepository<ScheduledAudit> _scheduleRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<AuditIssue> _issueRepository;
    private readonly INotificationBroadcaster _broadcaster;
    private readonly IOutbox _outbox;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _config;
    private readonly ILogger<AuditNotificationService> _logger;

    public AuditNotificationService(
        IRepository<ScheduledAudit> scheduleRepository,
        IRepository<Customer> customerRepository,
        IRepository<AuditIssue> issueRepository,
        INotificationBroadcaster broadcaster,
        IOutbox outbox,
        IEmailSender emailSender,
        IConfiguration config,
        ILogger<AuditNotificationService> logger)
    {
        _scheduleRepository = scheduleRepository;
        _customerRepository = customerRepository;
        _issueRepository = issueRepository;
        _broadcaster = broadcaster;
        _outbox = outbox;
        _emailSender = emailSender;
        _config = config;
        _logger = logger;
    }

    public Task NotifyCompletedAsync(AuditRun run, CancellationToken cancellationToken = default)
        => NotifyAsync(run, succeeded: true, cancellationToken);

    public Task NotifyFailedAsync(AuditRun run, CancellationToken cancellationToken = default)
        => NotifyAsync(run, succeeded: false, cancellationToken);

    private async Task NotifyAsync(AuditRun run, bool succeeded, CancellationToken cancellationToken)
    {
        var webhookUrl = _config["Audit:CompletionWebhookUrl"];
        var notifyEmail = _config.GetValue("Audit:NotifyEmailOnComplete", true);
        var notifyOnCriticalOnly = _config.GetValue("Audit:NotifyOnCriticalOnly", false);

        if (run.ScheduledAuditId.HasValue)
        {
            var schedule = await _scheduleRepository.Table
                .FirstOrDefaultAsync(s => s.Id == run.ScheduledAuditId.Value, cancellationToken);

            if (schedule != null)
            {
                if (!string.IsNullOrWhiteSpace(schedule.WebhookUrl))
                    webhookUrl = schedule.WebhookUrl.Trim();
                notifyEmail = schedule.NotifyOnComplete;
                notifyOnCriticalOnly = schedule.NotifyOnCriticalOnly;
            }
        }

        if (succeeded && notifyOnCriticalOnly && run.CriticalCount <= 0)
        {
            _logger.LogDebug(
                "Skipping audit notification for {EntityId}: critical-only mode, no critical issues",
                run.EntityId);
            return;
        }

        CriticalSummary? criticalSummary = succeeded
            ? await BuildCriticalSummaryAsync(run, cancellationToken)
            : null;

        if (run.CustomerId.HasValue)
        {
            var title = succeeded
                ? run.CriticalCount > 0
                    ? $"Kritik SEO sorunları: {run.NormalizedUrl}"
                    : "SEO denetimi tamamlandı"
                : "SEO denetimi başarısız";

            var message = succeeded
                ? BuildUserMessage(run, criticalSummary)
                : $"{run.NormalizedUrl} — {run.ErrorMessage ?? "Bilinmeyen hata"}";

            var severity = !succeeded ? "error"
                : run.CriticalCount > 0 ? "warning"
                : "info";

            try
            {
                await _broadcaster.NotifyUserAsync(run.CustomerId.Value, title, message, severity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR audit notification failed for customer {CustomerId}", run.CustomerId);
            }
        }

        if (!string.IsNullOrWhiteSpace(webhookUrl))
            await EnqueueWebhookAsync(run, webhookUrl, succeeded, criticalSummary, cancellationToken);

        if (notifyEmail && run.CustomerId.HasValue)
            await SendEmailAsync(run, succeeded, criticalSummary, cancellationToken);
    }

    private async Task<CriticalSummary?> BuildCriticalSummaryAsync(
        AuditRun run, CancellationToken cancellationToken)
    {
        if (run.CriticalCount <= 0) return null;

        var criticalIssues = await _issueRepository.Table
            .Where(i => i.AuditRunId == run.Id && i.Severity == AuditIssueSeverity.Critical)
            .ToListAsync(cancellationToken);

        var schedDiff = await _issueRepository.Table
            .Where(i => i.AuditRunId == run.Id && i.RuleId == "SCHED-002")
            .ToListAsync(cancellationToken);

        var newRules = AuditReportService.ExtractNewCriticalRuleIds(schedDiff);

        var rules = criticalIssues
            .GroupBy(i => i.RuleId)
            .Select(g => new CriticalRuleSummary(
                g.Key,
                g.First().Message,
                g.Count(),
                newRules.Contains(g.Key)))
            .OrderByDescending(r => r.IsNew)
            .ThenByDescending(r => r.PageCount)
            .Take(10)
            .ToList();

        return new CriticalSummary(rules, newRules.Count);
    }

    private static string BuildUserMessage(AuditRun run, CriticalSummary? criticalSummary)
    {
        var baseMsg = $"{run.NormalizedUrl} — skor {run.Score}/100, {run.CriticalCount} kritik, {run.WarningCount} uyarı.";
        if (criticalSummary == null || criticalSummary.Rules.Count == 0)
            return baseMsg;

        var top = string.Join(", ", criticalSummary.Rules.Take(3).Select(r => r.RuleId));
        var newHint = criticalSummary.NewRuleCount > 0
            ? $" {criticalSummary.NewRuleCount} yeni kritik kural."
            : "";
        return $"{baseMsg} Öncelik: {top}.{newHint}";
    }

    private async Task EnqueueWebhookAsync(
        AuditRun run,
        string webhookUrl,
        bool succeeded,
        CriticalSummary? criticalSummary,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            @event = succeeded ? "audit.completed" : "audit.failed",
            auditRunEntityId = run.EntityId,
            url = run.NormalizedUrl,
            status = run.Status.ToString(),
            score = run.Score,
            criticalCount = run.CriticalCount,
            warningCount = run.WarningCount,
            infoCount = run.InfoCount,
            pagesCrawled = run.PagesCrawled,
            scheduledAuditId = run.ScheduledAuditId,
            completedAt = run.CompletedAt,
            errorMessage = run.ErrorMessage,
            criticalRules = criticalSummary?.Rules.Select(r => new
            {
                ruleId = r.RuleId,
                message = r.Message,
                pageCount = r.PageCount,
                isNewSinceLastRun = r.IsNew,
            }),
            newCriticalRuleCount = criticalSummary?.NewRuleCount ?? 0,
        });

        try
        {
            await _outbox.EnqueueAsync(new OutboxEnqueue
            {
                MessageType = "webhook.audit.completed",
                Target = webhookUrl,
                Payload = payload,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue audit webhook for {EntityId}", run.EntityId);
        }
    }

    private async Task SendEmailAsync(
        AuditRun run,
        bool succeeded,
        CriticalSummary? criticalSummary,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.Table
            .FirstOrDefaultAsync(c => c.Id == run.CustomerId!.Value, cancellationToken);

        if (customer == null || string.IsNullOrWhiteSpace(customer.Email)) return;

        var publicUrl = (_config["App:PublicUrl"] ?? "http://localhost:4200").TrimEnd('/');
        var reportUrl = $"{publicUrl}/?audit={run.EntityId}";
        var criticalReportUrl = $"{publicUrl}/api/v1/public/audit/{run.EntityId}/export?format=critical";

        var subject = succeeded
            ? run.CriticalCount > 0
                ? $"Kritik SEO sorunları ({run.CriticalCount}): {run.NormalizedUrl}"
                : $"SEO denetimi tamamlandı: {run.NormalizedUrl} ({run.Score}/100)"
            : $"SEO denetimi başarısız: {run.NormalizedUrl}";

        var body = new StringBuilder();
        body.AppendLine("<html><body style=\"font-family:sans-serif;line-height:1.5\">");
        body.AppendLine($"<h2>{System.Net.WebUtility.HtmlEncode(subject)}</h2>");
        body.AppendLine("<ul>");
        body.AppendLine($"<li><strong>URL:</strong> {System.Net.WebUtility.HtmlEncode(run.NormalizedUrl)}</li>");
        body.AppendLine($"<li><strong>Durum:</strong> {run.Status}</li>");
        if (succeeded)
        {
            body.AppendLine($"<li><strong>Skor:</strong> {run.Score}/100</li>");
            body.AppendLine($"<li><strong>Kritik / Uyarı / Bilgi:</strong> {run.CriticalCount} / {run.WarningCount} / {run.InfoCount}</li>");
            body.AppendLine($"<li><strong>Taranan sayfa:</strong> {run.PagesCrawled}</li>");
        }
        else
        {
            body.AppendLine($"<li><strong>Hata:</strong> {System.Net.WebUtility.HtmlEncode(run.ErrorMessage ?? "—")}</li>");
        }
        body.AppendLine("</ul>");

        if (succeeded && criticalSummary is { Rules.Count: > 0 })
        {
            body.AppendLine("<h3>Kritik sorunlar (özet)</h3><ul>");
            foreach (var rule in criticalSummary.Rules)
            {
                var prefix = rule.IsNew ? "[YENİ] " : "";
                body.AppendLine(
                    $"<li>{prefix}<strong>{System.Net.WebUtility.HtmlEncode(rule.RuleId)}</strong> — " +
                    $"{System.Net.WebUtility.HtmlEncode(rule.Message)} ({rule.PageCount} sayfa)</li>");
            }
            body.AppendLine("</ul>");
            body.AppendLine(
                $"<p><a href=\"{System.Net.WebUtility.HtmlEncode(criticalReportUrl)}\">Kritik sorunlar raporu (HTML/PDF)</a></p>");
        }

        body.AppendLine($"<p><a href=\"{System.Net.WebUtility.HtmlEncode(reportUrl)}\">Tam raporu görüntüle</a></p>");
        body.AppendLine("</body></html>");

        try
        {
            await _emailSender.SendAsync(new EmailMessage(
                customer.Email,
                subject,
                body.ToString()), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit completion email failed for {Email}", customer.Email);
        }
    }

    private sealed record CriticalRuleSummary(string RuleId, string Message, int PageCount, bool IsNew);

    private sealed record CriticalSummary(IList<CriticalRuleSummary> Rules, int NewRuleCount);
}
