namespace SearchConsoleApp.Core.RequestScope;

/// <summary>
/// Her isteğin ambient context'i. Audience, tenant, user bilgisini taşır.
///
/// Service'ler bunu DI ile alır — manuel parametre geçmek yerine.
/// Cache key factory, event publisher, logger enricher, metrics tag'ler
/// bunu okur ve cross-cutting concern'leri otomatik audience-aware yapar.
///
/// Lifetime: Scoped (request başına). HTTP context yoksa (background job)
/// kod manuel olarak `IRequestScopeMutator` ile set eder.
/// </summary>
public interface IRequestScope
{
    /// <summary>Bu request hangi audience'a hizmet ediyor?</summary>
    Audience Audience { get; }

    /// <summary>Multi-tenant aktifse aktif tenant. Yoksa null.</summary>
    long? TenantId { get; }

    /// <summary>Authenticated kullanıcı varsa onun Id'si.</summary>
    long? CustomerId { get; }

    /// <summary>Authenticated kullanıcının EntityId'si (public ID).</summary>
    Guid? CustomerEntityId { get; }

    /// <summary>Aktif DeviceSession'ın Id'si. JWT 'sid' claim'inden.</summary>
    long? SessionId { get; }

    /// <summary>Trace ID — log ve metric'lerde request'leri bağlamak için.</summary>
    string? CorrelationId { get; }
}

/// <summary>
/// Background job, message consumer, scheduled task gibi HTTP olmayan
/// senaryolarda scope'u manuel set etmek için.
/// </summary>
public interface IRequestScopeMutator
{
    IDisposable BeginScope(Audience audience, long? tenantId = null, long? customerId = null);
}
