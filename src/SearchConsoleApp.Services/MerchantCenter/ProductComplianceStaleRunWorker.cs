using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.MerchantCenter;

/// <summary>
/// Pending/Crawling/Analyzing durumunda takılı kalan ürün uyumluluk analizlerini zaman aşımına uğratır.
/// </summary>
public class ProductComplianceStaleRunWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ProductComplianceStaleRunWorker> _logger;

    public ProductComplianceStaleRunWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ProductComplianceStaleRunWorker> logger)
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
                _logger.LogError(ex, "Product compliance stale run check failed");
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
        var service = scope.ServiceProvider.GetRequiredService<IProductComplianceService>();

        var stale = await context.Set<ProductComplianceRun>()
            .Where(r =>
                r.Status == ProductComplianceRunStatus.Pending && r.CreatedAt < now.AddMinutes(-pendingMinutes)
                || r.Status == ProductComplianceRunStatus.Crawling && (r.StartedAt ?? r.CreatedAt) < now.AddMinutes(-crawlingMinutes)
                || r.Status == ProductComplianceRunStatus.Analyzing && (r.StartedAt ?? r.CreatedAt) < now.AddMinutes(-analyzingMinutes))
            .Select(r => r.EntityId)
            .ToListAsync(cancellationToken);

        foreach (var entityId in stale)
        {
            _logger.LogWarning("Failing stale product compliance run {EntityId}", entityId);
            await service.FailCrawlAsync(
                entityId,
                "Analiz zaman aşımına uğradı. Crawl worker çalışıyor mu kontrol edin.",
                cancellationToken);
        }
    }
}
