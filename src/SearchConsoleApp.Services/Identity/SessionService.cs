using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Domain.Identity;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Core.Realtime;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Identity;

public partial class SessionService : ISessionService, IScopedService
{
    private readonly IRepository<DeviceSession> _repo;
    private readonly IGeoIpService _geoIp;
    private readonly INotificationBroadcaster _broadcaster;

    public SessionService(
        IRepository<DeviceSession> repo,
        IGeoIpService geoIp,
        INotificationBroadcaster broadcaster)
    {
        _repo = repo;
        _geoIp = geoIp;
        _broadcaster = broadcaster;
    }

    public virtual async Task<DeviceSession> StartAsync(
        long customerId, long deviceId, Audience audience,
        string refreshTokenHash, string? ip, string? userAgent)
    {
        var now = DateTime.UtcNow;
        var geo = _geoIp.Lookup(ip);

        var session = new DeviceSession
        {
            CustomerId = customerId,
            DeviceId = deviceId,
            Audience = audience.ToSlug(),
            RefreshTokenHash = refreshTokenHash,
            IpAddress = ip,
            IpCountry = geo?.CountryCode,
            IpCity = geo?.City,
            UserAgent = userAgent,
            StartedUtc = now,
            LastActivityUtc = now,
        };
        await _repo.InsertAsync(session);
        return session;
    }

    public virtual async Task UpdateActivityAsync(string refreshTokenHash)
    {
        // ExecuteUpdate ile tek SQL — entity yüklemeden
        await _repo.Table
            .Where(s => s.RefreshTokenHash == refreshTokenHash && s.RevokedUtc == null)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(s => s.LastActivityUtc, DateTime.UtcNow));
    }

    public virtual async Task RevokeAsync(long sessionId, string reason, long? revokedByCustomerId = null)
    {
        var session = await _repo.GetByIdAsync(sessionId);
        if (session == null || session.RevokedUtc != null) return;

        session.RevokedUtc = DateTime.UtcNow;
        session.RevokedReason = reason;
        session.RevokedByCustomerId = revokedByCustomerId;
        await _repo.UpdateAsync(session);

        // Client'ı bilgilendir — açık tab/uygulama hemen logout olsun
        await _broadcaster.SessionRevokedAsync(session.CustomerId, session.Id, reason);
    }

    public virtual async Task RevokeByRefreshTokenAsync(string refreshTokenHash, string reason)
    {
        var session = await _repo.Table
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash && s.RevokedUtc == null);
        if (session == null) return;

        session.RevokedUtc = DateTime.UtcNow;
        session.RevokedReason = reason;
        await _repo.UpdateAsync(session);

        // Rotation reason'da broadcast YAPMA — refresh akışı normal, kullanıcı atılmamalı
        if (reason != "rotation")
        {
            await _broadcaster.SessionRevokedAsync(session.CustomerId, session.Id, reason);
        }
    }

    public virtual async Task RevokeAllExceptAsync(long customerId, long exceptSessionId, string reason)
    {
        var now = DateTime.UtcNow;
        await _repo.Table
            .Where(s => s.CustomerId == customerId && s.Id != exceptSessionId && s.RevokedUtc == null)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(s => s.RevokedUtc, now)
                .SetProperty(s => s.RevokedReason, reason));
    }

    public virtual Task<IList<DeviceSession>> GetActiveSessionsAsync(long customerId)
        => _repo.GetAllAsync(q => q
            .Where(s => s.CustomerId == customerId && s.RevokedUtc == null)
            .OrderByDescending(s => s.LastActivityUtc));

    public virtual Task<IList<DeviceSession>> GetAllSessionsAsync(long customerId, int take = 100)
        => _repo.GetAllAsync(q => q
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.StartedUtc)
            .Take(take));
}
