using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

/// <summary>
/// Zamanlanmış SEO denetimlerini periyodik olarak tetikler.
/// </summary>
public class ScheduledAuditWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ScheduledAuditWorker> _logger;
    private readonly TimeSpan _pollInterval;

    public ScheduledAuditWorker(
        IServiceProvider services,
        IConfiguration config,
        ILogger<ScheduledAuditWorker> logger)
    {
        _services = services;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(config.GetValue("Audit:Schedule:PollIntervalSeconds", 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        _logger.LogInformation("Scheduled audit worker started (interval={Interval}s)", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueSchedulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled audit worker batch error");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ProcessDueSchedulesAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();
        var scheduleService = scope.ServiceProvider.GetRequiredService<IScheduledAuditService>();

        var now = DateTime.UtcNow;
        var dueIds = await context.Set<ScheduledAudit>()
            .Where(s => s.IsEnabled && s.NextRunUtc <= now)
            .OrderBy(s => s.NextRunUtc)
            .Select(s => s.Id)
            .Take(10)
            .ToListAsync(ct);

        foreach (var id in dueIds)
        {
            var schedule = await context.Set<ScheduledAudit>().FirstOrDefaultAsync(s => s.Id == id, ct);
            if (schedule == null || !schedule.IsEnabled) continue;

            try
            {
                await scheduleService.TriggerDueRunAsync(schedule, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger scheduled audit {EntityId}", schedule.EntityId);
                schedule.NextRunUtc = now.AddHours(1);
                schedule.UpdatedAtUtc = now;
                await context.SaveChangesAsync(ct);
            }
        }
    }
}
