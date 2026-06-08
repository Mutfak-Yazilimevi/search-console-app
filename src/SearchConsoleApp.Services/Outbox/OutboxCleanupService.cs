using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Outbox;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Outbox;

/// <summary>
/// OutboxMessage retention:
/// - `succeeded` mesajlar belirli süre sonra silinir (debug için kısa retention)
/// - `dead` mesajlar daha uzun tutulur (post-mortem analiz için)
///
/// Config:
///   "Outbox:Retention:SucceededDays" = 7
///   "Outbox:Retention:DeadDays" = 90
///   "Outbox:Retention:IntervalHours" = 24
///
/// Dead mesajlar manuel retry/delete edilebilir (admin UI'dan) — otomatik
/// silmek yerine retention çok uzun (3 ay). Operasyon ekibi karar verir.
/// </summary>
public class OutboxCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxCleanupService> _logger;
    private readonly TimeSpan _interval;
    private readonly int _succeededDays;
    private readonly int _deadDays;

    public OutboxCleanupService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<OutboxCleanupService> logger)
    {
        _services = services;
        _logger = logger;
        _interval = TimeSpan.FromHours(config.GetValue("Outbox:Retention:IntervalHours", 24));
        _succeededDays = config.GetValue("Outbox:Retention:SucceededDays", 7);
        _deadDays = config.GetValue("Outbox:Retention:DeadDays", 90);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);   // cold start

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox cleanup hatası");
            }
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();

        var succeededCutoff = DateTime.UtcNow.AddDays(-_succeededDays);
        var deadCutoff = DateTime.UtcNow.AddDays(-_deadDays);

        var succeededDeleted = await ctx.Set<OutboxMessage>()
            .Where(m => m.Status == "succeeded" && m.CompletedUtc < succeededCutoff)
            .ExecuteDeleteAsync(ct);

        var deadDeleted = await ctx.Set<OutboxMessage>()
            .Where(m => m.Status == "dead" && m.CompletedUtc < deadCutoff)
            .ExecuteDeleteAsync(ct);

        if (succeededDeleted > 0 || deadDeleted > 0)
        {
            _logger.LogInformation(
                "Outbox cleanup: {Succeeded} succeeded silindi (>{SDays}gün), {Dead} dead silindi (>{DDays}gün)",
                succeededDeleted, _succeededDays, deadDeleted, _deadDays);
        }
    }
}
