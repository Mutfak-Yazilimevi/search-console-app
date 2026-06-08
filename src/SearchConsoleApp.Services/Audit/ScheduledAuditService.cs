using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IScheduledAuditService
{
    Task<IList<ScheduledAudit>> ListAsync(long customerId, CancellationToken cancellationToken = default);
    Task<ScheduledAudit?> GetAsync(Guid entityId, long customerId, CancellationToken cancellationToken = default);
    Task<ScheduledAudit> CreateAsync(long customerId, CreateScheduledAuditRequest request, CancellationToken cancellationToken = default);
    Task<ScheduledAudit?> UpdateAsync(Guid entityId, long customerId, UpdateScheduledAuditRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid entityId, long customerId, CancellationToken cancellationToken = default);
    Task<IList<AuditDashboardSiteRow>> GetDashboardAsync(long customerId, CancellationToken cancellationToken = default);
    Task<IList<AuditHistoryRow>> GetHistoryAsync(long customerId, string? normalizedUrl, int limit, CancellationToken cancellationToken = default);
    Task TriggerDueRunAsync(ScheduledAudit schedule, CancellationToken cancellationToken = default);
}

public record CreateScheduledAuditRequest(
    string Url,
    string? Label,
    string? SearchConsolePropertyUrl,
    string? MigrationSourceUrl,
    string? Ga4PropertyId,
    string? WebhookUrl,
    bool NotifyOnComplete,
    bool NotifyOnCriticalOnly,
    int IntervalDays);

public record UpdateScheduledAuditRequest(
    string? Label,
    string? SearchConsolePropertyUrl,
    string? MigrationSourceUrl,
    string? Ga4PropertyId,
    string? WebhookUrl,
    bool? NotifyOnComplete,
    bool? NotifyOnCriticalOnly,
    int? IntervalDays,
    bool? IsEnabled);

public record AuditDashboardSiteRow(
    string NormalizedUrl,
    string? Label,
    Guid? ScheduleEntityId,
    bool ScheduleEnabled,
    int? LatestScore,
    int LatestCriticalCount,
    int LatestWarningCount,
    DateTime? LastCompletedAt,
    Guid? LastAuditEntityId,
    int? ScoreDelta,
    int TotalRuns);

public record AuditHistoryRow(
    Guid EntityId,
    string NormalizedUrl,
    string Status,
    int? Score,
    int CriticalCount,
    int WarningCount,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    bool IsScheduled);

public partial class ScheduledAuditService : IScheduledAuditService, IScopedService
{
    private readonly IRepository<ScheduledAudit> _scheduleRepository;
    private readonly IRepository<AuditRun> _auditRunRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<ScheduledAuditService> _logger;

    public ScheduledAuditService(
        IRepository<ScheduledAudit> scheduleRepository,
        IRepository<AuditRun> auditRunRepository,
        IAuditService auditService,
        ILogger<ScheduledAuditService> logger)
    {
        _scheduleRepository = scheduleRepository;
        _auditRunRepository = auditRunRepository;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<IList<ScheduledAudit>> ListAsync(long customerId, CancellationToken cancellationToken = default)
        => await _scheduleRepository.Table
            .Where(s => s.CustomerId == customerId)
            .OrderBy(s => s.Url)
            .ToListAsync(cancellationToken);

    public async Task<ScheduledAudit?> GetAsync(Guid entityId, long customerId, CancellationToken cancellationToken = default)
        => await _scheduleRepository.Table
            .FirstOrDefaultAsync(s => s.EntityId == entityId && s.CustomerId == customerId, cancellationToken);

    public async Task<ScheduledAudit> CreateAsync(
        long customerId, CreateScheduledAuditRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            throw new ArgumentException("URL is required.", nameof(request));

        var interval = Math.Clamp(request.IntervalDays, 1, 90);
        var now = DateTime.UtcNow;

        var schedule = new ScheduledAudit
        {
            CustomerId = customerId,
            Label = request.Label?.Trim(),
            Url = request.Url.Trim(),
            SearchConsolePropertyUrl = request.SearchConsolePropertyUrl?.Trim(),
            MigrationSourceUrl = request.MigrationSourceUrl?.Trim(),
            Ga4PropertyId = request.Ga4PropertyId?.Trim(),
            WebhookUrl = string.IsNullOrWhiteSpace(request.WebhookUrl) ? null : request.WebhookUrl.Trim(),
            NotifyOnComplete = request.NotifyOnComplete,
            NotifyOnCriticalOnly = request.NotifyOnCriticalOnly,
            IntervalDays = interval,
            NextRunUtc = now,
            IsEnabled = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        await _scheduleRepository.InsertAsync(schedule, publishEvent: false);
        return schedule;
    }

    public async Task<ScheduledAudit?> UpdateAsync(
        Guid entityId, long customerId, UpdateScheduledAuditRequest request, CancellationToken cancellationToken = default)
    {
        var schedule = await GetAsync(entityId, customerId, cancellationToken);
        if (schedule == null) return null;

        if (request.Label != null) schedule.Label = request.Label.Trim();
        if (request.SearchConsolePropertyUrl != null) schedule.SearchConsolePropertyUrl = request.SearchConsolePropertyUrl.Trim();
        if (request.MigrationSourceUrl != null) schedule.MigrationSourceUrl = string.IsNullOrWhiteSpace(request.MigrationSourceUrl) ? null : request.MigrationSourceUrl.Trim();
        if (request.Ga4PropertyId != null) schedule.Ga4PropertyId = string.IsNullOrWhiteSpace(request.Ga4PropertyId) ? null : request.Ga4PropertyId.Trim();
        if (request.WebhookUrl != null) schedule.WebhookUrl = string.IsNullOrWhiteSpace(request.WebhookUrl) ? null : request.WebhookUrl.Trim();
        if (request.NotifyOnComplete.HasValue) schedule.NotifyOnComplete = request.NotifyOnComplete.Value;
        if (request.NotifyOnCriticalOnly.HasValue) schedule.NotifyOnCriticalOnly = request.NotifyOnCriticalOnly.Value;
        if (request.IntervalDays.HasValue) schedule.IntervalDays = Math.Clamp(request.IntervalDays.Value, 1, 90);
        if (request.IsEnabled.HasValue) schedule.IsEnabled = request.IsEnabled.Value;

        schedule.UpdatedAtUtc = DateTime.UtcNow;
        await _scheduleRepository.UpdateAsync(schedule, publishEvent: false);
        return schedule;
    }

    public async Task<bool> DeleteAsync(Guid entityId, long customerId, CancellationToken cancellationToken = default)
    {
        var schedule = await GetAsync(entityId, customerId, cancellationToken);
        if (schedule == null) return false;

        await _scheduleRepository.HardDeleteAsync(schedule);
        return true;
    }

    public async Task<IList<AuditDashboardSiteRow>> GetDashboardAsync(
        long customerId, CancellationToken cancellationToken = default)
    {
        var schedules = await _scheduleRepository.Table
            .Where(s => s.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        var runs = await _auditRunRepository.Table
            .Where(r => r.CustomerId == customerId && r.Status == AuditRunStatus.Completed)
            .OrderByDescending(r => r.CompletedAt)
            .ToListAsync(cancellationToken);

        var byHost = runs
            .GroupBy(r => new Uri(r.NormalizedUrl).Host.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<AuditDashboardSiteRow>();

        foreach (var schedule in schedules)
        {
            var normalized = AuditUrlNormalizer.Normalize(schedule.Url);
            var host = new Uri(normalized).Host.ToLowerInvariant();
            byHost.TryGetValue(host, out var hostRuns);
            hostRuns ??= [];

            var latest = hostRuns.FirstOrDefault();
            var previous = hostRuns.Skip(1).FirstOrDefault();
            var scoreDelta = latest?.Score != null && previous?.Score != null
                ? latest.Score - previous.Score
                : null;

            rows.Add(new AuditDashboardSiteRow(
                normalized,
                schedule.Label,
                schedule.EntityId,
                schedule.IsEnabled,
                latest?.Score,
                latest?.CriticalCount ?? 0,
                latest?.WarningCount ?? 0,
                latest?.CompletedAt,
                latest?.EntityId,
                scoreDelta,
                hostRuns.Count));
        }

        var scheduledHosts = rows.Select(r => new Uri(r.NormalizedUrl).Host.ToLowerInvariant()).ToHashSet();
        foreach (var group in byHost.Where(g => !scheduledHosts.Contains(g.Key)))
        {
            var latest = group.Value[0];
            var previous = group.Value.Skip(1).FirstOrDefault();
            rows.Add(new AuditDashboardSiteRow(
                latest.NormalizedUrl,
                null,
                null,
                false,
                latest.Score,
                latest.CriticalCount,
                latest.WarningCount,
                latest.CompletedAt,
                latest.EntityId,
                latest.Score != null && previous?.Score != null ? latest.Score - previous.Score : null,
                group.Value.Count));
        }

        return rows.OrderByDescending(r => r.LastCompletedAt).ToList();
    }

    public async Task<IList<AuditHistoryRow>> GetHistoryAsync(
        long customerId, string? normalizedUrl, int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query = _auditRunRepository.Table.Where(r => r.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(normalizedUrl))
        {
            var normalized = AuditUrlNormalizer.Normalize(normalizedUrl);
            var host = new Uri(normalized).Host;
            query = query.Where(r => r.NormalizedUrl.Contains(host));
        }

        var runs = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return runs.Select(r => new AuditHistoryRow(
            r.EntityId,
            r.NormalizedUrl,
            r.Status.ToString(),
            r.Score,
            r.CriticalCount,
            r.WarningCount,
            r.CreatedAt,
            r.CompletedAt,
            r.ScheduledAuditId.HasValue)).ToList();
    }

    public async Task TriggerDueRunAsync(ScheduledAudit schedule, CancellationToken cancellationToken = default)
    {
        var active = await _auditRunRepository.Table.AnyAsync(r =>
            r.ScheduledAuditId == schedule.Id
            && (r.Status == AuditRunStatus.Pending
                || r.Status == AuditRunStatus.Crawling
                || r.Status == AuditRunStatus.Analyzing),
            cancellationToken);

        if (active)
        {
            _logger.LogInformation("Scheduled audit {EntityId} skipped — run already in progress", schedule.EntityId);
            return;
        }

        var run = await _auditService.StartScheduledAuditAsync(
            schedule.Url,
            schedule.CustomerId,
            schedule.Id,
            schedule.SearchConsolePropertyUrl,
            cancellationToken);

        schedule.LastAuditRunId = run.Id;
        schedule.NextRunUtc = DateTime.UtcNow.AddDays(schedule.IntervalDays);
        schedule.UpdatedAtUtc = DateTime.UtcNow;
        await _scheduleRepository.UpdateAsync(schedule, publishEvent: false);

        _logger.LogInformation(
            "Scheduled audit triggered: schedule={ScheduleId}, run={RunId}, next={NextRun}",
            schedule.EntityId, run.EntityId, schedule.NextRunUtc);
    }
}
