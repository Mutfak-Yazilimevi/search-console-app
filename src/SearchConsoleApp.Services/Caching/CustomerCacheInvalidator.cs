using SearchConsoleApp.Core.Caching;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Events;

namespace SearchConsoleApp.Services.Caching;

/// <summary>
/// Customer state değişiminde TÜM audience'lar için cache'i temizler.
///
/// Önemli: Admin bir Customer güncellediğinde web ve public projeksiyonları
/// da invalid olur — `AllAudiencePrefixesFor` ile her audience prefix'i
/// temizlenir.
///
/// Bu pattern başka entity'ler için de tekrar edilir. Generic versiyon
/// yazılabilir (`AudienceAwareCacheInvalidator<T>`) ama açık tutmak daha
/// okunabilir.
/// </summary>
public class CustomerCacheInvalidator :
    IConsumer<EntityInsertedEvent<Customer>>,
    IConsumer<EntityUpdatedEvent<Customer>>,
    IConsumer<EntityDeletedEvent<Customer>>
{
    private readonly IStaticCacheManager _cache;
    private readonly ICacheKeyFactory _keys;

    public CustomerCacheInvalidator(IStaticCacheManager cache, ICacheKeyFactory keys)
    {
        _cache = cache;
        _keys = keys;
    }

    public Task HandleEventAsync(EntityInsertedEvent<Customer> eventMessage)
        => InvalidateAllAudiencesAsync();

    public Task HandleEventAsync(EntityUpdatedEvent<Customer> eventMessage)
        => InvalidateAllAudiencesAsync();

    public Task HandleEventAsync(EntityDeletedEvent<Customer> eventMessage)
        => InvalidateAllAudiencesAsync();

    private async Task InvalidateAllAudiencesAsync()
    {
        foreach (var prefix in _keys.AllAudiencePrefixesFor<Customer>())
            await _cache.RemoveByPrefixAsync(prefix);
    }
}
