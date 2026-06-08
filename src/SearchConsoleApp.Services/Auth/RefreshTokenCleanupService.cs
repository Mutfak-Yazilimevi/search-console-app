using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Auth;

/// <summary>
/// Süresi geçmiş veya revoke edilmiş refresh token'ları DB'den temizler.
/// 30 günlük token süresi × her cihaz × her refresh = milyonlarca kayıt potansiyeli.
///
/// Her 24 saatte bir çalışır. 30 günden eski (süresi geçmiş veya revoke edilmiş)
/// kayıtları siler — audit için süre uzatılabilir.
/// </summary>
public class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<RefreshTokenCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24);
    private readonly TimeSpan _retention = TimeSpan.FromDays(30);

    public RefreshTokenCleanupService(IServiceProvider services, ILogger<RefreshTokenCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // İlk gecikme — startup sırasında yoğunluk yapma
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token cleanup hatası");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();

        var cutoff = DateTime.UtcNow - _retention;

        // EF Core 7+ ExecuteDeleteAsync — tek SQL, kayıt yüklemiyor
        var deleted = await context.Set<RefreshToken>()
            .Where(t => t.ExpiresOnUtc < cutoff || (t.RevokedOnUtc != null && t.RevokedOnUtc < cutoff))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            _logger.LogInformation("RefreshToken cleanup: {Count} kayıt silindi.", deleted);
    }
}
