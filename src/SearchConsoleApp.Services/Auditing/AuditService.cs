using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Domain.Auditing;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Core.Realtime;
using SearchConsoleApp.Core.RequestScope;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Customers;

namespace SearchConsoleApp.Services.Auditing;

public partial class AuditService : IAuditService, IScopedService
{
    private readonly IRepository<AuditLog> _repo;
    private readonly IRequestScope _scope;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICustomerService _customerService;
    private readonly INotificationBroadcaster _broadcaster;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IRepository<AuditLog> repo,
        IRequestScope scope,
        IHttpContextAccessor httpContextAccessor,
        ICustomerService customerService,
        INotificationBroadcaster broadcaster,
        ILogger<AuditService> logger)
    {
        _repo = repo;
        _scope = scope;
        _httpContextAccessor = httpContextAccessor;
        _customerService = customerService;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public virtual async Task LogAsync(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Action);

        var http = _httpContextAccessor.HttpContext;
        var (ip, ua) = ExtractClientInfo(http);

        // Actor email lookup — kullanıcı varsa email'i de logla
        string? actorEmail = null;
        if (_scope.CustomerId.HasValue)
        {
            var customer = await _customerService.GetCustomerByIdAsync(_scope.CustomerId.Value);
            actorEmail = customer?.Email;
        }

        var log = new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            Audience = _scope.Audience.ToSlug(),

            ActorCustomerId = _scope.CustomerId,
            ActorEmail = actorEmail,
            ActorIp = ip,
            ActorUserAgent = ua,
            ActorSessionId = _scope.SessionId,
            // ActorDeviceId — Session lookup ile dolar; AuditLog'ı yazarken bunu
            // her seferinde session join etmek pahalı. Audit query yapılırken
            // SessionId üzerinden Device'a JOIN edilebilir.

            Action = entry.Action,
            TargetType = entry.TargetType,
            TargetId = entry.TargetId,
            TargetEntityId = entry.TargetEntityId,

            ChangesJson = entry.ChangesJson,
            MetadataJson = entry.MetadataJson,
            Outcome = entry.Outcome,
            FailureReason = entry.FailureReason,

            CorrelationId = _scope.CorrelationId,
            TenantId = _scope.TenantId,
        };

        try
        {
            // publishEvent: false → AuditLog'a yapılan insert'in kendisi audit'lenmesin
            // (sonsuz döngü koruması)
            await _repo.InsertAsync(log, publishEvent: false);

            // Admin paneline canlı feed — fail-tolerant
            try
            {
                await _broadcaster.AuditEventAsync(new AuditEventBroadcast(
                    log.Id, log.Timestamp, log.Audience,
                    log.ActorCustomerId, log.ActorEmail,
                    log.Action, log.TargetType, log.TargetId,
                    log.Outcome));
            }
            catch (Exception bcastEx)
            {
                _logger.LogWarning(bcastEx, "Audit broadcast başarısız (kayıt yine de yazıldı)");
            }
        }
        catch (Exception ex)
        {
            // Audit yazımı başarısızsa: business action'ı bozma, ama log'a düş.
            // Production'da Serilog → Elastic'e yansır, fark edilir.
            _logger.LogError(ex,
                "AuditLog yazılamadı. Action: {Action}, Target: {TargetType}/{TargetId}",
                entry.Action, entry.TargetType, entry.TargetId);
        }
    }

    public virtual async Task<IList<AuditLog>> QueryAsync(AuditQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = _repo.Table.AsNoTracking();

        if (query.ActorCustomerId.HasValue)
            q = q.Where(a => a.ActorCustomerId == query.ActorCustomerId.Value);
        if (!string.IsNullOrEmpty(query.TargetType))
            q = q.Where(a => a.TargetType == query.TargetType);
        if (query.TargetId.HasValue)
            q = q.Where(a => a.TargetId == query.TargetId.Value);
        if (query.Audience.HasValue)
            q = q.Where(a => a.Audience == query.Audience.Value.ToSlug());
        if (!string.IsNullOrEmpty(query.Action))
            q = q.Where(a => a.Action == query.Action);
        if (query.FromUtc.HasValue)
            q = q.Where(a => a.Timestamp >= query.FromUtc.Value);
        if (query.ToUtc.HasValue)
            q = q.Where(a => a.Timestamp <= query.ToUtc.Value);

        return await q
            .OrderByDescending(a => a.Timestamp)
            .Skip(query.Skip)
            .Take(Math.Min(query.Take, 1000))
            .ToListAsync();
    }

    private static (string? Ip, string? UserAgent) ExtractClientInfo(HttpContext? http)
    {
        if (http == null) return (null, null);
        var ip = http.Connection.RemoteIpAddress?.ToString();
        var ua = http.Request.Headers.UserAgent.ToString();
        return (ip, string.IsNullOrWhiteSpace(ua) ? null : ua);
    }
}
