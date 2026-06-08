using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface IAuditQuotaService
{
    Task<AuditQuotaResult> ValidateStartAsync(long? customerId, CancellationToken cancellationToken = default);
}

public record AuditQuotaResult(bool Allowed, string? Message = null);

public partial class AuditQuotaService : IAuditQuotaService, IScopedService
{
    private readonly IRepository<AuditRun> _auditRunRepository;
    private readonly IConfiguration _config;

    public AuditQuotaService(
        IRepository<AuditRun> auditRunRepository,
        IConfiguration config)
    {
        _auditRunRepository = auditRunRepository;
        _config = config;
    }

    public async Task<AuditQuotaResult> ValidateStartAsync(
        long? customerId, CancellationToken cancellationToken = default)
    {
        var maxConcurrentGlobal = _config.GetValue("Audit:Quota:MaxConcurrentGlobal", 100);
        var maxConcurrentPerCustomer = _config.GetValue("Audit:Quota:MaxConcurrentPerCustomer", 5);
        var maxDailyAnonymousGlobal = _config.GetValue("Audit:Quota:MaxDailyAnonymousGlobal", 200);
        var maxDailyPerCustomer = _config.GetValue("Audit:Quota:MaxDailyPerCustomer", 100);

        var activeStatuses = new[]
        {
            AuditRunStatus.Pending,
            AuditRunStatus.Crawling,
            AuditRunStatus.Analyzing,
        };

        var globalActive = await _auditRunRepository.Table
            .CountAsync(r => activeStatuses.Contains(r.Status), cancellationToken);

        if (globalActive >= maxConcurrentGlobal)
            return new AuditQuotaResult(false, "Sistem yoğun — lütfen birkaç dakika sonra tekrar deneyin.");

        if (customerId == null)
        {
            var dayStart = DateTime.UtcNow.Date;
            var dailyAnonymous = await _auditRunRepository.Table
                .CountAsync(r => r.Mode == AuditMode.Anonymous && r.CreatedAt >= dayStart, cancellationToken);

            if (dailyAnonymous >= maxDailyAnonymousGlobal)
                return new AuditQuotaResult(false, "Günlük anonim tarama limitine ulaşıldı. Giriş yaparak devam edebilirsiniz.");
        }

        if (customerId.HasValue)
        {
            var customerActive = await _auditRunRepository.Table
                .CountAsync(r => r.CustomerId == customerId
                    && activeStatuses.Contains(r.Status), cancellationToken);

            if (customerActive >= maxConcurrentPerCustomer)
                return new AuditQuotaResult(false, "Hesabınız için eşzamanlı tarama limitine ulaşıldı.");

            var dayStart = DateTime.UtcNow.Date;
            var dailyCustomer = await _auditRunRepository.Table
                .CountAsync(r => r.CustomerId == customerId && r.CreatedAt >= dayStart, cancellationToken);

            if (dailyCustomer >= maxDailyPerCustomer)
                return new AuditQuotaResult(false, "Günlük tarama limitine ulaşıldı.");
        }

        return new AuditQuotaResult(true);
    }
}
