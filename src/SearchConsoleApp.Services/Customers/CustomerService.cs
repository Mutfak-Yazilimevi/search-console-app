using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Caching;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Events;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Customers;

/// <summary>
/// ÖRNEK service. ICacheKeyFactory ile audience-aware cache.
///
/// Aynı method farklı audience'lardan çağrılınca farklı cache key üretir:
///   Admin'den: "SearchConsoleApp.admin.customer.byid.42"
///   Web'den:   "SearchConsoleApp.web.customer.byid.42"
///
/// Bu sayede cache'de "admin'in zengin verisi web'e sızıyor" sorunu olmaz.
/// Servis tek bir method yazar, audience prefix otomatik gelir.
/// </summary>
public partial class CustomerService : ICustomerService, IScopedService
{
    private readonly IRepository<Customer> _customerRepository;
    private readonly IStaticCacheManager _cacheManager;
    private readonly ICacheKeyFactory _cacheKeys;
    private readonly IEventPublisher _eventPublisher;

    public CustomerService(
        IRepository<Customer> customerRepository,
        IStaticCacheManager cacheManager,
        ICacheKeyFactory cacheKeys,
        IEventPublisher eventPublisher)
    {
        _customerRepository = customerRepository;
        _cacheManager = cacheManager;
        _cacheKeys = cacheKeys;
        _eventPublisher = eventPublisher;
    }

    public virtual async Task<Customer?> GetCustomerByIdAsync(long customerId)
    {
        if (customerId == 0) return null;
        var key = _cacheKeys.For<Customer>("byid", customerId);
        return await _cacheManager.GetAsync(key,
            async () => await _customerRepository.GetByIdAsync(customerId));
    }

    public virtual async Task<Customer?> GetCustomerByEntityIdAsync(Guid entityId)
    {
        if (entityId == Guid.Empty) return null;
        var key = _cacheKeys.For<Customer>("byentityid", entityId);
        return await _cacheManager.GetAsync(key,
            async () => await _customerRepository.GetByEntityIdAsync(entityId));
    }

    public virtual async Task<Customer?> GetCustomerByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var key = _cacheKeys.For<Customer>("byemail", email.ToLowerInvariant());
        return await _cacheManager.GetAsync(key, async () =>
            await _customerRepository.Table.FirstOrDefaultAsync(c => c.Email == email));
    }

    public virtual async Task<IList<Customer>> GetAllCustomersAsync(bool onlyActive = true)
    {
        return await _customerRepository.GetAllAsync(query =>
        {
            if (onlyActive) query = query.Where(c => c.Active);
            return query.OrderBy(c => c.Id);
        });
    }

    public virtual async Task InsertCustomerAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        await _customerRepository.InsertAsync(customer);
        // Cache invalidation otomatik: CustomerCacheInvalidator consumer'ı
        // EntityInsertedEvent'i yakalar ve TÜM audience'lar için temizler.
    }

    public virtual async Task UpdateCustomerAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        await _customerRepository.UpdateAsync(customer);
    }

    public virtual async Task DeleteCustomerAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        await _customerRepository.DeleteAsync(customer);
    }
}
