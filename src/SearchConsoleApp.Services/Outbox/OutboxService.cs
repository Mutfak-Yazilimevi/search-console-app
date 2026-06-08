using System.Text.Json;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Domain.Outbox;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Core.RequestScope;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Outbox;

public partial class OutboxService : IOutbox, IScopedService
{
    private readonly IRepository<OutboxMessage> _repo;
    private readonly IRequestScope _scope;

    public OutboxService(IRepository<OutboxMessage> repo, IRequestScope scope)
    {
        _repo = repo;
        _scope = scope;
    }

    public virtual async Task EnqueueAsync(OutboxEnqueue message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.MessageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Target);

        var entity = new OutboxMessage
        {
            MessageType = message.MessageType,
            Target = message.Target,
            Payload = message.Payload ?? "{}",
            HeadersJson = message.Headers != null && message.Headers.Count > 0
                ? JsonSerializer.Serialize(message.Headers)
                : null,
            CreatedOnUtc = DateTime.UtcNow,
            AvailableAtUtc = message.AvailableAtUtc,
            Status = "pending",
            AttemptCount = 0,
            Audience = _scope.Audience.ToSlug(),
            TenantId = _scope.TenantId,
            CorrelationId = _scope.CorrelationId,
        };

        // publishEvent: false → outbox kaydı için event publish'lemeye gerek yok
        // (sonsuz döngü riski; outbox kendi başına event'tir)
        await _repo.InsertAsync(entity, publishEvent: false);
    }
}
