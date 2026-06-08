using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Outbox;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Outbox;

/// <summary>
/// Outbox dispatcher background worker.
///
/// Polling pattern:
/// 1. WHERE Status='pending' AND (AvailableAtUtc IS NULL OR AvailableAtUtc &lt;= now)
///    ORDER BY CreatedOnUtc LIMIT batchSize
/// 2. Her mesaj için handler bul → SendAsync çağır
/// 3. Success → Status='succeeded', CompletedUtc set
/// 4. Transient fail → AttemptCount++, AvailableAtUtc=now+backoff, Status='pending'
/// 5. PermanentException veya max retry → Status='dead'
///
/// Backoff: 30s, 2dk, 10dk, 1h, 6h, 24h — exponential ceiling 24h
/// MaxAttempts: 8 (sonra dead-letter)
///
/// Multi-instance dispatcher: aynı mesajı iki pod almasın diye claim
/// mekanizması var — `UPDATE ... WHERE Status='pending' SET Status='in_progress'`
/// EF Core ExecuteUpdateAsync ile atomic. Sonra in_progress mesajları işle.
///
/// Performance:
/// - PollIntervalSeconds: 5 (default)
/// - BatchSize: 50 (default)
/// - Stuck 'in_progress' temizliği: 5dk timeout (process crash sonrası)
/// </summary>
public class OutboxDispatcherService : BackgroundService
{
    private static readonly TimeSpan[] BackoffSteps = new[]
    {
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(24),
    };

    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxDispatcherService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;
    private readonly int _maxAttempts;
    private readonly TimeSpan _claimTimeout;
    private readonly string _workerId;

    public OutboxDispatcherService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<OutboxDispatcherService> logger)
    {
        _services = services;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(config.GetValue("Outbox:PollIntervalSeconds", 5));
        _batchSize = config.GetValue("Outbox:BatchSize", 50);
        _maxAttempts = config.GetValue("Outbox:MaxAttempts", 8);
        _claimTimeout = TimeSpan.FromMinutes(config.GetValue("Outbox:ClaimTimeoutMinutes", 5));
        _workerId = Environment.MachineName + ":" + Guid.NewGuid().ToString()[..8];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Cold start delay
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        _logger.LogInformation("Outbox dispatcher başladı: workerId={WorkerId}", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                {
                    // İş yoksa bekle
                    await Task.Delay(_pollInterval, stoppingToken);
                }
                // İş varsa hemen tekrar dene — backlog'u bitir
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatcher batch hatası");
                await Task.Delay(_pollInterval, stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();
        var handlers = scope.ServiceProvider.GetServices<IOutboxMessageHandler>().ToList();

        // 1. Stuck 'in_progress' mesajları sıfırla (crash recovery)
        var stuckCutoff = DateTime.UtcNow - _claimTimeout;
        await context.Set<OutboxMessage>()
            .Where(m => m.Status == "in_progress" && m.LastAttemptUtc < stuckCutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "pending"), ct);

        // 2. Pending'leri çek (sadece ID'ler — claim için)
        var now = DateTime.UtcNow;
        var pendingIds = await context.Set<OutboxMessage>()
            .Where(m => m.Status == "pending"
                     && (m.AvailableAtUtc == null || m.AvailableAtUtc <= now))
            .OrderBy(m => m.CreatedOnUtc)
            .Select(m => m.Id)
            .Take(_batchSize)
            .ToListAsync(ct);

        if (pendingIds.Count == 0) return 0;

        // 3. Claim — atomic update. Race condition'da diğer worker'lar farklı row alır.
        var claimedCount = await context.Set<OutboxMessage>()
            .Where(m => pendingIds.Contains(m.Id) && m.Status == "pending")
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, "in_progress")
                .SetProperty(m => m.LastAttemptUtc, now), ct);

        if (claimedCount == 0) return 0;

        // 4. Claim'lenmiş mesajları çek, işle
        var messages = await context.Set<OutboxMessage>()
            .Where(m => pendingIds.Contains(m.Id) && m.Status == "in_progress")
            .ToListAsync(ct);

        foreach (var msg in messages)
        {
            await ProcessMessageAsync(context, handlers, msg, ct);
        }

        return messages.Count;
    }

    private async Task ProcessMessageAsync(
        SearchConsoleAppDbContext context,
        List<IOutboxMessageHandler> handlers,
        OutboxMessage message,
        CancellationToken ct)
    {
        var handler = handlers.FirstOrDefault(h => h.CanHandle(message.MessageType));

        if (handler == null)
        {
            _logger.LogError("Outbox handler bulunamadı: {Type}, msgId={Id}",
                message.MessageType, message.Id);
            message.Status = "dead";
            message.LastError = $"No handler for type: {message.MessageType}";
            message.CompletedUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
            return;
        }

        message.AttemptCount++;
        message.LastAttemptUtc = DateTime.UtcNow;

        try
        {
            await handler.SendAsync(message, ct);

            message.Status = "succeeded";
            message.CompletedUtc = DateTime.UtcNow;
            message.LastError = null;
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("Outbox sent: type={Type}, target={Target}, attempts={Attempts}",
                message.MessageType, message.Target, message.AttemptCount);
        }
        catch (OutboxPermanentException ex)
        {
            // Kalıcı hata — dead'e at
            message.Status = "dead";
            message.LastError = ex.Message;
            message.CompletedUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);

            _logger.LogWarning("Outbox dead-letter (permanent): type={Type}, target={Target}, error={Error}",
                message.MessageType, message.Target, ex.Message);
        }
        catch (Exception ex)
        {
            // Transient hata — retry'a al
            if (message.AttemptCount >= _maxAttempts)
            {
                message.Status = "dead";
                message.LastError = $"Max attempts ({_maxAttempts}) exceeded. Last error: {ex.Message}";
                message.CompletedUtc = DateTime.UtcNow;

                _logger.LogWarning("Outbox dead-letter (max attempts): type={Type}, error={Error}",
                    message.MessageType, ex.Message);
            }
            else
            {
                var backoff = BackoffSteps[Math.Min(message.AttemptCount - 1, BackoffSteps.Length - 1)];
                message.Status = "pending";
                message.AvailableAtUtc = DateTime.UtcNow.Add(backoff);
                message.LastError = ex.Message;

                _logger.LogInformation(
                    "Outbox retry scheduled: type={Type}, attempt={Attempt}/{Max}, next in {Backoff}",
                    message.MessageType, message.AttemptCount, _maxAttempts, backoff);
            }
            await context.SaveChangesAsync(ct);
        }
    }
}
