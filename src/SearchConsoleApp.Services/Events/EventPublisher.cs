using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Events;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Core.RequestScope;

namespace SearchConsoleApp.Services.Events;

/// <summary>
/// In-process event publisher. Consumer'ları paralel çalıştırır.
///
/// İki audience-aware davranış:
/// 1. Yayınlama sırasında: IAudienceAware event'lere mevcut audience set edilir
///    (IRequestScope'tan okunur).
/// 2. Tüketim sırasında: IConsumerAudienceFilter implement eden consumer'lar
///    sadece izin verilen audience'lardan gelen event'leri alır.
/// </summary>
public class EventPublisher : IEventPublisher, IScopedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IRequestScope _scope;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(
        IServiceProvider serviceProvider,
        IRequestScope scope,
        ILogger<EventPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _scope = scope;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T eventMessage)
    {
        if (eventMessage == null) return;

        // Audience-aware ise mevcut scope'u event'e işle
        if (eventMessage is IAudienceAware audienceAware)
        {
            // reflection yerine pattern matching — generic constraint olmadığı için
            // property atayalım. (Performans için: tüm event'lerde aynı pattern)
            audienceAware.GetType().GetProperty(nameof(IAudienceAware.SourceAudience))
                ?.SetValue(audienceAware, _scope.Audience);
        }

        var consumers = _serviceProvider.GetServices<IConsumer<T>>().ToList();
        if (consumers.Count == 0) return;

        var sourceAudience = (eventMessage as IAudienceAware)?.SourceAudience;

        var tasks = consumers.Select(async consumer =>
        {
            // Audience filter kontrolü
            if (sourceAudience.HasValue && consumer is IConsumerAudienceFilter filter)
            {
                var allowed = filter.AllowedAudiences;
                if (allowed != null && allowed.Count > 0 && !allowed.Contains(sourceAudience.Value))
                    return; // Bu audience'ı dinlemiyor — atla
            }

            try
            {
                await consumer.HandleEventAsync(eventMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Event consumer hatası: {Consumer} → {Event} (audience: {Audience})",
                    consumer.GetType().FullName,
                    typeof(T).Name,
                    sourceAudience?.ToString() ?? "n/a");
            }
        });

        await Task.WhenAll(tasks);
    }
}
