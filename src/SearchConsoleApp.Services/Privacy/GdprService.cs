using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Domain.Auditing;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Domain.Identity;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Auditing;
using SearchConsoleApp.Services.Auth;

namespace SearchConsoleApp.Services.Privacy;

public partial class GdprService : IGdprService, IScopedService
{
    private readonly IRepository<Customer> _customerRepo;
    private readonly IRepository<AuditLog> _auditRepo;
    private readonly IRepository<DeviceSession> _sessionRepo;
    private readonly IRepository<Device> _deviceRepo;
    private readonly IRepository<ExternalLogin> _externalRepo;
    private readonly IAuditService _auditService;
    private readonly IAuthService _authService;

    public GdprService(
        IRepository<Customer> customerRepo,
        IRepository<AuditLog> auditRepo,
        IRepository<DeviceSession> sessionRepo,
        IRepository<Device> deviceRepo,
        IRepository<ExternalLogin> externalRepo,
        IAuditService auditService,
        IAuthService authService)
    {
        _customerRepo = customerRepo;
        _auditRepo = auditRepo;
        _sessionRepo = sessionRepo;
        _deviceRepo = deviceRepo;
        _externalRepo = externalRepo;
        _auditService = auditService;
        _authService = authService;
    }

    public virtual async Task<string> ExportCustomerDataAsync(long customerId)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException("Customer bulunamadı.");

        var sessions = await _sessionRepo.GetAllAsync(q => q.Where(s => s.CustomerId == customerId));
        var devices = await _deviceRepo.GetAllAsync(q => q.Where(d => d.CustomerId == customerId));
        var externals = await _externalRepo.GetAllAsync(q => q.Where(e => e.CustomerId == customerId));
        var auditLogs = await _auditRepo.GetAllAsync(q => q
            .Where(a => a.ActorCustomerId == customerId)
            .OrderBy(a => a.Timestamp)
            .Take(10000));

        var export = new
        {
            customer = new
            {
                customer.Email,
                customer.FirstName,
                customer.LastName,
                customer.Username,
                customer.Roles,
                customer.EmailConfirmed,
                customer.TwoFactorEnabled,
                customer.CreatedOnUtc,
                customer.LastLoginUtc,
            },
            externalLogins = externals.Select(e => new
            {
                e.Provider, e.Email, e.DisplayName, e.LinkedOnUtc, e.LastLoginUtc
            }),
            devices = devices.Select(d => new
            {
                d.Name, d.DeviceType, d.FirstSeenUtc, d.LastSeenUtc
                // Fingerprint hariç (PII)
            }),
            sessions = sessions.Select(s => new
            {
                s.Audience, s.IpAddress, s.IpCountry, s.IpCity,
                s.UserAgent, s.StartedUtc, s.LastActivityUtc,
                s.RevokedUtc, s.RevokedReason
            }),
            auditLogs = auditLogs.Select(a => new
            {
                a.Timestamp, a.Action, a.TargetType, a.TargetId, a.Outcome
                // ActorIp/UA dahil değil — kullanıcı zaten kendisi
            }),
            exportedOnUtc = DateTime.UtcNow,
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true,
            // API genelinde camelCase kullanılıyor — export da tutarlı olmalı.
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }

    public virtual async Task AnonymizeCustomerAsync(long customerId, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var customer = await _customerRepo.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException("Customer bulunamadı.");

        if (customer.Deleted)
            return;  // Idempotent

        var anonymousEmail = $"deleted-{customerId}@anonymized.local";

        // 1. Customer PII'ı temizle ve soft-delete
        customer.Email = anonymousEmail;
        customer.FirstName = null;
        customer.LastName = null;
        customer.Username = null;
        customer.PasswordHash = null;
        customer.TotpSecret = null;
        customer.RecoveryCodesHashes = null;
        customer.Active = false;
        customer.Deleted = true;
        await _customerRepo.UpdateAsync(customer, publishEvent: false);
        // publishEvent:false → silinen customer için event publish'e gerek yok,
        // GDPR audit zaten manuel yazılıyor

        // 3. DeviceSession PII'ı temizle
        await _sessionRepo.Table
            .Where(s => s.CustomerId == customerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IpAddress, (string?)null)
                .SetProperty(x => x.UserAgent, (string?)null)
                .SetProperty(x => x.IpCity, (string?)null));
        // IpCountry korunur — coğrafi istatistik için kişisel olmayan

        // 4. Device kayıtlarını hard delete (fingerprint kişiyle ilişkilendirilebilir)
        var devices = await _deviceRepo.Table.Where(d => d.CustomerId == customerId).ToListAsync();
        foreach (var d in devices)
        {
            await _deviceRepo.HardDeleteAsync(d, publishEvent: false);
        }

        // 5. ExternalLogin'leri hard delete
        var externals = await _externalRepo.Table.Where(e => e.CustomerId == customerId).ToListAsync();
        foreach (var e in externals)
        {
            await _externalRepo.HardDeleteAsync(e, publishEvent: false);
        }

        // 6. Tüm aktif session'ları revoke
        await _authService.RevokeAllAsync(customerId);

        // 7. Manuel audit kaydı — kim sildi, ne zaman, neden
        await _auditService.LogAsync(new AuditEntry
        {
            Action = "gdpr.anonymize",
            TargetType = "Customer",
            TargetId = customerId,
            MetadataJson = JsonSerializer.Serialize(new { reason }),
        });

        // 8. AuditLog'lardaki PII'ı temizle — action/target kalır, kim olduğu silinir.
        // EN SON çalışır: yukarıdaki gdpr.anonymize kaydının ActorEmail'ini de
        // null'lar. (AuditService cache'li customer lookup yaptığı için manuel
        // kayda eski email düşebilir; bu scrub onu da garanti temizler.)
        await _auditRepo.Table
            .Where(a => a.ActorCustomerId == customerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.ActorEmail, (string?)null)
                .SetProperty(a => a.ActorIp, (string?)null)
                .SetProperty(a => a.ActorUserAgent, (string?)null));
    }
}
