using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Auth;
using SearchConsoleApp.Services.Auditing;
using SearchConsoleApp.Services.Identity;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;
using SearchConsoleApp.Web.Framework.Auth;

namespace SearchConsoleApp.Web.Controllers.Admin;

public record AuditLogDto(
    long Id,
    DateTime Timestamp,
    string Audience,
    long? ActorCustomerId,
    string? ActorEmail,
    string? ActorIp,
    string? ActorUserAgent,
    string Action,
    string? TargetType,
    long? TargetId,
    Guid? TargetEntityId,
    string Outcome,
    string? FailureReason,
    string? ChangesJson,
    string? CorrelationId);

public record AuditQueryParams(
    long? ActorCustomerId,
    string? TargetType,
    long? TargetId,
    string? Audience,
    string? Action,
    DateTime? From,
    DateTime? To,
    int Take = 100,
    int Skip = 0);

/// <summary>
/// Admin audit log sorgulama.
/// Route: /api/v1/admin/audit/*
/// Permission: audit.read
/// </summary>
[HasPermission(Permissions.AuditRead)]
public class AuditController : AdminApiController
{
    private readonly IAuditService _auditService;
    public AuditController(IAuditService auditService) => _auditService = auditService;

    /// <summary>Audit log sorgu. Kullanıcı/entity/action/tarih filtreleri.</summary>
    [HttpGet]
    public async Task<IActionResult> Query([FromQuery] AuditQueryParams p)
    {
        Audience? aud = null;
        if (!string.IsNullOrEmpty(p.Audience) &&
            Enum.TryParse<Audience>(p.Audience, ignoreCase: true, out var parsed))
        {
            aud = parsed;
        }

        var logs = await _auditService.QueryAsync(new AuditQuery
        {
            ActorCustomerId = p.ActorCustomerId,
            TargetType = p.TargetType,
            TargetId = p.TargetId,
            Audience = aud,
            Action = p.Action,
            FromUtc = p.From,
            ToUtc = p.To,
            Take = p.Take,
            Skip = p.Skip,
        });

        return Ok(logs.Select(a => new AuditLogDto(
            a.Id, a.Timestamp, a.Audience,
            a.ActorCustomerId, a.ActorEmail, a.ActorIp, a.ActorUserAgent,
            a.Action, a.TargetType, a.TargetId, a.TargetEntityId,
            a.Outcome, a.FailureReason, a.ChangesJson, a.CorrelationId)));
    }

    /// <summary>Belirli bir kullanıcının son aktivitesi.</summary>
    [HttpGet("customers/{customerId:long}")]
    public async Task<IActionResult> ByCustomer(long customerId, [FromQuery] int take = 100)
    {
        var logs = await _auditService.QueryAsync(new AuditQuery
        {
            ActorCustomerId = customerId,
            Take = take,
        });
        return Ok(logs);
    }

    /// <summary>Belirli bir entity'nin değişim geçmişi.</summary>
    [HttpGet("entity/{targetType}/{targetId:long}")]
    public async Task<IActionResult> ByEntity(string targetType, long targetId, [FromQuery] int take = 100)
    {
        var logs = await _auditService.QueryAsync(new AuditQuery
        {
            TargetType = targetType,
            TargetId = targetId,
            Take = take,
        });
        return Ok(logs);
    }
}

/// <summary>
/// Admin: kullanıcı oturumlarını görüntüleme + zorla kapatma.
/// </summary>
public class AdminSessionsController : AdminApiController
{
    private readonly ISessionService _sessions;
    private readonly Core.RequestScope.IRequestScope _scope;

    public AdminSessionsController(ISessionService sessions, Core.RequestScope.IRequestScope scope)
    {
        _sessions = sessions;
        _scope = scope;
    }

    /// <summary>Belirli bir kullanıcının aktif oturumları.</summary>
    [HttpGet("customers/{customerId:long}/sessions")]
    public async Task<IActionResult> CustomerActiveSessions(long customerId)
    {
        var sessions = await _sessions.GetActiveSessionsAsync(customerId);
        return Ok(sessions);
    }

    /// <summary>Belirli bir kullanıcının geçmişi (son 100).</summary>
    [HttpGet("customers/{customerId:long}/sessions/history")]
    public async Task<IActionResult> CustomerSessionHistory(long customerId)
    {
        var sessions = await _sessions.GetAllSessionsAsync(customerId);
        return Ok(sessions);
    }

    /// <summary>Admin: bir kullanıcının belirli oturumunu kapat.</summary>
    [HttpPost("sessions/{sessionId:long}/revoke")]
    [Audit("admin.session.revoke", TargetType = "DeviceSession", TargetIdRouteKey = "sessionId")]
    public async Task<IActionResult> RevokeSession(long sessionId)
    {
        var adminId = _scope.CustomerId;
        await _sessions.RevokeAsync(sessionId, "admin", adminId);
        return Ok(new { ok = true });
    }

    /// <summary>Admin: kullanıcının tüm oturumlarını kapat ("hesap askıya alındı").</summary>
    [HttpPost("customers/{customerId:long}/sessions/revoke-all")]
    [Audit("admin.session.revoke_all", TargetType = "Customer", TargetIdRouteKey = "customerId")]
    public async Task<IActionResult> RevokeAllForCustomer(long customerId)
    {
        await _sessions.RevokeAllExceptAsync(customerId, exceptSessionId: 0, reason: "admin");
        return Ok(new { ok = true });
    }
}
