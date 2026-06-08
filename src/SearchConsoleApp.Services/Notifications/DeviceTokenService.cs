using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Domain.Notifications;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Notifications;

public partial class DeviceTokenService : IDeviceTokenService, IScopedService
{
    private readonly IRepository<DeviceToken> _repo;

    public DeviceTokenService(IRepository<DeviceToken> repo) => _repo = repo;

    public virtual async Task RegisterAsync(
        long customerId, string token, string provider, string platform,
        string? deviceName, string? appVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var now = DateTime.UtcNow;

        var existing = await _repo.Table
            .FirstOrDefaultAsync(t => t.CustomerId == customerId && t.Token == token);

        if (existing != null)
        {
            existing.LastSeenUtc = now;
            existing.Platform = platform;
            existing.DeviceName = deviceName;
            existing.AppVersion = appVersion;
            await _repo.UpdateAsync(existing, publishEvent: false);
            return;
        }

        await _repo.InsertAsync(new DeviceToken
        {
            CustomerId = customerId,
            Token = token,
            Provider = provider,
            Platform = platform,
            DeviceName = deviceName,
            AppVersion = appVersion,
            CreatedOnUtc = now,
            LastSeenUtc = now,
        });
    }

    public virtual async Task UnregisterAsync(long customerId, string token)
    {
        var existing = await _repo.Table
            .FirstOrDefaultAsync(t => t.CustomerId == customerId && t.Token == token);
        if (existing != null) await _repo.DeleteAsync(existing);
    }

    public virtual async Task<IList<DeviceToken>> GetByCustomerAsync(long customerId)
    {
        return await _repo.Table
            .Where(t => t.CustomerId == customerId)
            .ToListAsync();
    }
}
