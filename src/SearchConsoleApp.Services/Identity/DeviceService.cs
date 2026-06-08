using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Domain.Identity;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Identity;

public partial class DeviceService : IDeviceService, IScopedService
{
    private readonly IRepository<Device> _repo;
    private readonly IDeviceFingerprintService _fingerprint;

    public DeviceService(IRepository<Device> repo, IDeviceFingerprintService fingerprint)
    {
        _repo = repo;
        _fingerprint = fingerprint;
    }

    public virtual async Task<Device> GetOrCreateAsync(long customerId, FingerprintInput input, string deviceType)
    {
        var fingerprint = _fingerprint.Compute(input);

        var existing = await _repo.Table
            .FirstOrDefaultAsync(d => d.CustomerId == customerId && d.Fingerprint == fingerprint);

        var now = DateTime.UtcNow;

        if (existing != null)
        {
            existing.LastSeenUtc = now;
            await _repo.UpdateAsync(existing, publishEvent: false);
            return existing;
        }

        var device = new Device
        {
            CustomerId = customerId,
            Fingerprint = fingerprint,
            DeviceType = deviceType,
            FirstUserAgent = input.UserAgent,
            Trusted = false,
            BiometricEnabled = false,
            FirstSeenUtc = now,
            LastSeenUtc = now,
        };
        await _repo.InsertAsync(device);
        return device;
    }

    public virtual Task<Device?> GetByIdAsync(long deviceId)
        => _repo.GetByIdAsync(deviceId)!;

    public virtual Task<Device?> GetByEntityIdAsync(Guid entityId)
        => _repo.GetByEntityIdAsync(entityId)!;

    public virtual Task<IList<Device>> GetByCustomerAsync(long customerId)
        => _repo.GetAllAsync(q => q.Where(d => d.CustomerId == customerId)
                                   .OrderByDescending(d => d.LastSeenUtc));

    public virtual async Task SetTrustedAsync(long deviceId, bool trusted)
    {
        var device = await _repo.GetByIdAsync(deviceId);
        if (device == null) return;
        device.Trusted = trusted;
        await _repo.UpdateAsync(device);
    }

    public virtual async Task RenameAsync(long deviceId, string newName)
    {
        var device = await _repo.GetByIdAsync(deviceId);
        if (device == null) return;
        device.Name = newName?.Trim();
        await _repo.UpdateAsync(device);
    }

    public virtual async Task DeleteAsync(long deviceId)
    {
        var device = await _repo.GetByIdAsync(deviceId);
        if (device == null) return;
        await _repo.DeleteAsync(device);
    }
}
