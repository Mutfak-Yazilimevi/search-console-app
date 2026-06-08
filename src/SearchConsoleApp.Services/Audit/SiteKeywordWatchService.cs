using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Audit;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Audit;

public interface ISiteKeywordWatchService
{
    Task<IList<SiteKeywordWatch>> ListAsync(long customerId, string? siteHost, CancellationToken cancellationToken = default);
    Task<SiteKeywordWatch> CreateAsync(long customerId, string siteUrl, string keyword, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid entityId, long customerId, CancellationToken cancellationToken = default);
}

public partial class SiteKeywordWatchService : ISiteKeywordWatchService, IScopedService
{
    private readonly IRepository<SiteKeywordWatch> _repository;

    public SiteKeywordWatchService(IRepository<SiteKeywordWatch> repository)
    {
        _repository = repository;
    }

    public async Task<IList<SiteKeywordWatch>> ListAsync(
        long customerId, string? siteHost, CancellationToken cancellationToken = default)
    {
        var query = _repository.Table.Where(w => w.CustomerId == customerId && w.IsEnabled);
        if (!string.IsNullOrWhiteSpace(siteHost))
        {
            var host = new Uri(AuditUrlNormalizer.Normalize(siteHost)).Host.ToLowerInvariant();
            query = query.Where(w => w.SiteHost == host);
        }

        return await query.OrderBy(w => w.SiteHost).ThenBy(w => w.Keyword).ToListAsync(cancellationToken);
    }

    public async Task<SiteKeywordWatch> CreateAsync(
        long customerId, string siteUrl, string keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(siteUrl))
            throw new ArgumentException("Site URL is required.", nameof(siteUrl));
        if (string.IsNullOrWhiteSpace(keyword))
            throw new ArgumentException("Keyword is required.", nameof(keyword));

        var host = new Uri(AuditUrlNormalizer.Normalize(siteUrl)).Host.ToLowerInvariant();
        var normalizedKeyword = keyword.Trim();

        var exists = await _repository.Table.AnyAsync(w =>
            w.CustomerId == customerId
            && w.SiteHost == host
            && w.Keyword == normalizedKeyword, cancellationToken);

        if (exists)
            throw new InvalidOperationException("This keyword is already tracked for the site.");

        var watch = new SiteKeywordWatch
        {
            CustomerId = customerId,
            SiteHost = host,
            Keyword = normalizedKeyword,
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _repository.InsertAsync(watch, publishEvent: false);
        return watch;
    }

    public async Task<bool> DeleteAsync(Guid entityId, long customerId, CancellationToken cancellationToken = default)
    {
        var watch = await _repository.Table
            .FirstOrDefaultAsync(w => w.EntityId == entityId && w.CustomerId == customerId, cancellationToken);
        if (watch == null) return false;

        await _repository.HardDeleteAsync(watch);
        return true;
    }
}
