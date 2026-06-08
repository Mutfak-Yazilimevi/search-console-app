using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

/// <summary>
/// Pending/Crawling durumunda takılı kalan denetimleri zaman aşımına uğratır.
/// </summary>
public class AuditStaleRunWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AuditStaleRunWorker> _logger;

    public AuditStaleRunWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<AuditStaleRunWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _config.GetValue("Audit:StaleCheckIntervalMinutes", 5);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FailStaleRunsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit stale run check failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    private async Task FailStaleRunsAsync(CancellationToken cancellationToken)
    {
        var pendingMinutes = _config.GetValue("Audit:PendingTimeoutMinutes", 10);
        var crawlingMinutes = _config.GetValue("Audit:CrawlingTimeoutMinutes", 45);
        var analyzingMinutes = _config.GetValue("Audit:AnalyzingTimeoutMinutes", 15);
        var now = DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var stale = await context.Set<AuditRun>()
            .Where(r => r.Status == AuditRunStatus.Pending && r.CreatedAt < now.AddMinutes(-pendingMinutes)
                || r.Status == AuditRunStatus.Crawling && (r.StartedAt ?? r.CreatedAt) < now.AddMinutes(-crawlingMinutes)
                || r.Status == AuditRunStatus.Analyzing && (r.StartedAt ?? r.CreatedAt) < now.AddMinutes(-analyzingMinutes))
            .Select(r => r.EntityId)
            .ToListAsync(cancellationToken);

        foreach (var entityId in stale)
        {
            _logger.LogWarning("Failing stale audit run {EntityId}", entityId);
            await auditService.FailCrawlAsync(
                entityId,
                "Tarama zaman aşımına uğradı. Crawl worker çalışıyor mu kontrol edin.",
                cancellationToken);
        }
    }
}
