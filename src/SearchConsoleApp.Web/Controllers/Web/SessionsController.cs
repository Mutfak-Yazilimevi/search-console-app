using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Core.RequestScope;
using SearchConsoleApp.Services.Identity;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;

namespace SearchConsoleApp.Web.Controllers.Web;

public record SessionDto(
    long Id,
    long DeviceId,
    string Audience,
    string? IpAddress,
    string? IpCountry,
    string? UserAgent,
    DateTime StartedUtc,
    DateTime LastActivityUtc,
    bool IsCurrent,
    bool IsActive,
    DateTime? RevokedUtc,
    string? RevokedReason);

public record DeviceDto(
    Guid EntityId,
    string? Name,
    string DeviceType,
    bool Trusted,
    DateTime FirstSeenUtc,
    DateTime LastSeenUtc,
    int ActiveSessionCount);

/// <summary>
/// Kullanıcının kendi oturumlarını ve cihazlarını yönetir.
/// Route: /api/web/sessions/*
/// </summary>
public class SessionsController : WebApiController
{
    private readonly ISessionService _sessions;
    private readonly IDeviceService _devices;
    private readonly IRequestScope _scope;

    public SessionsController(ISessionService sessions, IDeviceService devices, IRequestScope scope)
    {
        _sessions = sessions;
        _devices = devices;
        _scope = scope;
    }

    /// <summary>Aktif oturumlar (kullanıcının kendi).</summary>
    [HttpGet]
    public async Task<IActionResult> ListActive()
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var sessions = await _sessions.GetActiveSessionsAsync(customerId);
        var currentSessionId = _scope.SessionId;
        return Ok(sessions.Select(s => MapToDto(s, currentSessionId)));
    }

    /// <summary>Geçmiş dahil tüm oturumlar (son 100).</summary>
    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var sessions = await _sessions.GetAllSessionsAsync(customerId);
        var currentSessionId = _scope.SessionId;
        return Ok(sessions.Select(s => MapToDto(s, currentSessionId)));
    }

    /// <summary>Belirli bir oturumu kapat ("uzaktan logout").</summary>
    [HttpPost("{sessionId:long}/revoke")]
    [Audit("session.revoke", TargetType = "DeviceSession", TargetIdRouteKey = "sessionId")]
    public async Task<IActionResult> Revoke(long sessionId)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        await _sessions.RevokeAsync(sessionId, "user", customerId);
        return Ok(new { ok = true });
    }

    /// <summary>Mevcut oturum hariç tüm diğer oturumları kapat ("şüpheli aktivite").</summary>
    [HttpPost("revoke-others")]
    [Audit("session.revoke_others")]
    public async Task<IActionResult> RevokeOthers()
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        // Mevcut session JWT'den geliyor — bunu HARİÇ tut
        var currentSessionId = _scope.SessionId ?? 0;
        await _sessions.RevokeAllExceptAsync(customerId, currentSessionId, reason: "user");
        return Ok(new { ok = true });
    }

    /// <summary>Kullanıcının cihazları.</summary>
    [HttpGet("/api/web/devices")]
    public async Task<IActionResult> Devices()
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var devices = await _devices.GetByCustomerAsync(customerId);
        var sessions = await _sessions.GetActiveSessionsAsync(customerId);
        var activeCountByDevice = sessions
            .GroupBy(s => s.DeviceId)
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(devices.Select(d => new DeviceDto(
            d.EntityId, d.Name, d.DeviceType, d.Trusted,
            d.FirstSeenUtc, d.LastSeenUtc,
            activeCountByDevice.GetValueOrDefault(d.Id, 0))));
    }

    [HttpPatch("/api/web/devices/{entityId:guid}/rename")]
    [Audit("device.rename", TargetType = "Device", TargetIdRouteKey = "entityId")]
    public async Task<IActionResult> RenameDevice(Guid entityId, [FromBody] RenameDeviceRequest req)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var device = await _devices.GetByEntityIdAsync(entityId);
        if (device == null || device.CustomerId != customerId) return NotFoundResult();

        await _devices.RenameAsync(device.Id, req.Name);
        return Ok(new { ok = true });
    }

    [HttpPatch("/api/web/devices/{entityId:guid}/trust")]
    [Audit("device.trust", TargetType = "Device", TargetIdRouteKey = "entityId")]
    public async Task<IActionResult> TrustDevice(Guid entityId, [FromBody] TrustDeviceRequest req)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var device = await _devices.GetByEntityIdAsync(entityId);
        if (device == null || device.CustomerId != customerId) return NotFoundResult();

        await _devices.SetTrustedAsync(device.Id, req.Trusted);
        return Ok(new { ok = true });
    }

    // === Helpers ===

    private SessionDto MapToDto(Core.Domain.Identity.DeviceSession s, long? currentSessionId) => new(
        s.Id, s.DeviceId, s.Audience, s.IpAddress, s.IpCountry, s.UserAgent,
        s.StartedUtc, s.LastActivityUtc,
        IsCurrent: currentSessionId.HasValue && s.Id == currentSessionId.Value,
        IsActive: s.IsActive,
        s.RevokedUtc, s.RevokedReason);
}

public record RenameDeviceRequest(string Name);
public record TrustDeviceRequest(bool Trusted);
