using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Events;
using SearchConsoleApp.Core.RequestScope;

namespace SearchConsoleApp.Services.Auditing;

/// <summary>
/// ÖRNEK consumer: sadece admin audience'tan gelen Customer değişimlerini
/// audit log'a yazar. Public kullanıcının kendi kaydını update'lemesi
/// audit'e girmez (gürültü).
///
/// `IConsumerAudienceFilter` implement ederek hangi audience'lara tepki
/// vereceğini bildirir. EventPublisher otomatik filtrelenir.
/// </summary>
public class AdminCustomerAuditConsumer :
    IConsumer<EntityUpdatedEvent<Customer>>,
    IConsumer<EntityDeletedEvent<Customer>>,
    IConsumerAudienceFilter
{
    private readonly ILogger<AdminCustomerAuditConsumer> _logger;
    private readonly IRequestScope _scope;

    public AdminCustomerAuditConsumer(
        ILogger<AdminCustomerAuditConsumer> logger,
        IRequestScope scope)
    {
        _logger = logger;
        _scope = scope;
    }

    // Sadece Admin audience'ından gelen event'leri dinle
    public IReadOnlySet<Audience>? AllowedAudiences => new HashSet<Audience> { Audience.Admin };

    public Task HandleEventAsync(EntityUpdatedEvent<Customer> e)
    {
        _logger.LogInformation(
            "AUDIT: Admin {AdminId} updated Customer {CustomerEntityId} ({Email})",
            _scope.CustomerId, e.Entity.EntityId, e.Entity.Email);
        return Task.CompletedTask;
    }

    public Task HandleEventAsync(EntityDeletedEvent<Customer> e)
    {
        _logger.LogWarning(
            "AUDIT: Admin {AdminId} deleted Customer {CustomerEntityId} ({Email})",
            _scope.CustomerId, e.Entity.EntityId, e.Entity.Email);
        return Task.CompletedTask;
    }
}
