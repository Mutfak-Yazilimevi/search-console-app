using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Auditing;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Auditing;

/// <summary>
/// AuditLog ve DeviceSession için retention yönetimi.
///
/// İki strateji:
/// 1. **Delete-mode** (default): retention süresinden eski kayıtları siler.
/// 2. **Archive-mode**: eski kayıtları AuditLogArchive tablosuna taşır (history korunur).
///
/// Config:
///   Audit:Retention:Mode = "delete" | "archive"
///   Audit:Retention:AuditLogDays = 730  (2 yıl, hukuki min'e bağlı)
///   Audit:Retention:RevokedSessionDays = 90  (3 ay revoke edilmiş session)
///   Audit:Retention:IntervalHours = 24
///
/// Job 24 saatte bir çalışır, startup'ta 10 dakika bekler (cold-start gürültüsü olmasın).
///
/// Production'da archive-mode önerilir + ayrı index altında saklama. Şu an
/// archive-mode AuditLog'u SADECE log'lar (placeholder) — gerçek archive tablo
/// için ayrı entity ve mapping eklenir.
/// </summary>
public class AuditCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AuditCleanupService> _logger;
    private readonly TimeSpan _interval;
    private readonly int _auditRetentionDays;
    private readonly int _revokedSessionRetentionDays;
    private readonly string _mode;

    public AuditCleanupService(
        IServiceProvider services,
        IConfiguration config,
        ILogger<AuditCleanupService> logger)
    {
        _services = services;
        _logger = logger;
        _interval = TimeSpan.FromHours(config.GetValue("Audit:Retention:IntervalHours", 24));
        _auditRetentionDays = config.GetValue("Audit:Retention:AuditLogDays", 730);
        _revokedSessionRetentionDays = config.GetValue("Audit:Retention:RevokedSessionDays", 90);
        _mode = config["Audit:Retention:Mode"] ?? "delete";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit cleanup hatası");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();

        var auditCutoff = DateTime.UtcNow.AddDays(-_auditRetentionDays);
        var sessionCutoff = DateTime.UtcNow.AddDays(-_revokedSessionRetentionDays);

        if (_mode == "archive")
        {
            await ArchiveAuditLogsAsync(context, auditCutoff, ct);
        }
        else
        {
            var deletedAudits = await context.Set<AuditLog>()
                .Where(a => a.Timestamp < auditCutoff)
                .ExecuteDeleteAsync(ct);
            if (deletedAudits > 0)
                _logger.LogInformation("AuditLog cleanup: {Count} kayıt silindi (>{Days} gün)", deletedAudits, _auditRetentionDays);
        }

        // Revoke edilmiş session'lar — eski olanları sil (audit log onları zaten yakaladı)
        var deletedSessions = await context.Set<Core.Domain.Identity.DeviceSession>()
            .Where(s => s.RevokedUtc != null && s.RevokedUtc < sessionCutoff)
            .ExecuteDeleteAsync(ct);
        if (deletedSessions > 0)
            _logger.LogInformation("DeviceSession cleanup: {Count} revoked session silindi (>{Days} gün)",
                deletedSessions, _revokedSessionRetentionDays);
    }

    private async Task ArchiveAuditLogsAsync(SearchConsoleAppDbContext context, DateTime cutoff, CancellationToken ct)
    {
        const int batchSize = 1000;
        int totalArchived = 0;

        while (!ct.IsCancellationRequested)
        {
            // Batch oku — büyük tablolarda OOM olmasın
            var batch = await context.Set<AuditLog>()
                .AsNoTracking()
                .Where(a => a.Timestamp < cutoff)
                .OrderBy(a => a.Id)
                .Take(batchSize)
                .ToListAsync(ct);

            if (batch.Count == 0) break;

            // Archive tablosuna kopyala
            var archives = batch.Select(a => new AuditLogArchive
            {
                OriginalId = a.Id,
                Timestamp = a.Timestamp,
                Audience = a.Audience,
                ActorCustomerId = a.ActorCustomerId,
                ActorEmail = a.ActorEmail,
                ActorIp = a.ActorIp,
                ActorUserAgent = a.ActorUserAgent,
                ActorDeviceId = a.ActorDeviceId,
                ActorSessionId = a.ActorSessionId,
                Action = a.Action,
                TargetType = a.TargetType,
                TargetId = a.TargetId,
                TargetEntityId = a.TargetEntityId,
                ChangesJson = a.ChangesJson,
                MetadataJson = a.MetadataJson,
                Outcome = a.Outcome,
                FailureReason = a.FailureReason,
                CorrelationId = a.CorrelationId,
                TenantId = a.TenantId,
                ArchivedOnUtc = DateTime.UtcNow,
            }).ToList();

            await context.Set<AuditLogArchive>().AddRangeAsync(archives, ct);
            await context.SaveChangesAsync(ct);

            // Aynı batch'i aktif tablodan sil
            var idsToDelete = batch.Select(a => a.Id).ToList();
            await context.Set<AuditLog>()
                .Where(a => idsToDelete.Contains(a.Id))
                .ExecuteDeleteAsync(ct);

            totalArchived += batch.Count;
            _logger.LogInformation("AuditLog archive: batch {Count} kayıt taşındı (toplam {Total})",
                batch.Count, totalArchived);

            // Batch'ler arası kısa bekleme — DB nefes alsın
            if (batch.Count == batchSize)
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            else
                break;  // son batch
        }

        if (totalArchived > 0)
        {
            _logger.LogInformation("AuditLog archive tamamlandı: {Total} kayıt arşivlendi (>{Days} gün)",
                totalArchived, _auditRetentionDays);
        }
    }
}
