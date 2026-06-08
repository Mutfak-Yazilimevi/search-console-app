namespace SearchConsoleApp.Core.Events;

/// <summary>
/// Event tüketicisi. Tüm audience'lardan gelen event'leri dinler.
/// Sadece belirli audience istiyorsan ScopedConsumer<T> kullan.
/// </summary>
public interface IConsumer<T>
{
    Task HandleEventAsync(T eventMessage);
}

/// <summary>
/// Audience-aware event'i. Publisher yayınlarken hangi audience'tan
/// geldiğini ekler. Consumer'lar `Audiences` listesine bakarak filtrelenir.
///
/// Tüm IEventPublisher.PublishAsync çağrıları otomatik bunu wrap eder —
/// service kodu değişmez.
/// </summary>
public interface IAudienceAware
{
    Audience SourceAudience { get; }
}

/// <summary>
/// Bir consumer'ın hangi audience'lardan gelen event'lere tepki vereceğini
/// işaretler. Attribute olmamasının sebebi: composition test'ler için kolay.
/// </summary>
public interface IConsumerAudienceFilter
{
    /// <summary>Empty veya null = tüm audience'lar (default).</summary>
    IReadOnlySet<Audience>? AllowedAudiences { get; }
}

public interface IEventPublisher
{
    Task PublishAsync<T>(T eventMessage);
}

// === Standart entity event'leri ===

public class EntityInsertedEvent<T> : IAudienceAware where T : BaseEntity
{
    public T Entity { get; }
    public Audience SourceAudience { get; set; }
    public EntityInsertedEvent(T entity) => Entity = entity;
}

public class EntityUpdatedEvent<T> : IAudienceAware where T : BaseEntity
{
    public T Entity { get; }
    public Audience SourceAudience { get; set; }
    public EntityUpdatedEvent(T entity) => Entity = entity;
}

public class EntityDeletedEvent<T> : IAudienceAware where T : BaseEntity
{
    public T Entity { get; }
    public Audience SourceAudience { get; set; }
    public EntityDeletedEvent(T entity) => Entity = entity;
}
