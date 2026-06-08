using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Domain.Inbox;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Inbox;

/// <summary>
/// Inbox idempotency.
///
/// Kullanım pattern (webhook controller içinde):
///   var result = await _inbox.TryRecordAsync("stripe", eventId, eventType, payload);
///   if (result.AlreadyProcessed) return Ok();  // duplicate
///
///   await ProcessEventAsync(...);
///   await _inbox.MarkProcessedAsync(result.Id);
///
/// Race condition: unique constraint (Source, ExternalEventId) DB level
/// protection sağlar. İki istek aynı anda gelirse biri DbUpdateException alır
/// ve "duplicate" döner.
/// </summary>
public interface IInbox
{
    /// <summary>
    /// Yeni event'i kaydet. Duplicate ise AlreadyProcessed=true döner.
    /// </summary>
    Task<InboxRecordResult> TryRecordAsync(string source, string externalEventId,
                                            string eventType, string payload);

    Task MarkProcessedAsync(long id);
    Task MarkFailedAsync(long id, string error);
}

public record InboxRecordResult(long Id, bool AlreadyProcessed, InboxMessage? Existing);

public partial class InboxService : IInbox, IScopedService
{
    private readonly IRepository<InboxMessage> _repo;

    public InboxService(IRepository<InboxMessage> repo) => _repo = repo;

    public virtual async Task<InboxRecordResult> TryRecordAsync(
        string source, string externalEventId, string eventType, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalEventId);

        // Önce var mı kontrol (yaygın senaryo, exception ucuza patlamaz)
        var existing = await _repo.Table
            .FirstOrDefaultAsync(m => m.Source == source && m.ExternalEventId == externalEventId);

        if (existing != null)
        {
            return new InboxRecordResult(existing.Id, AlreadyProcessed: true, existing);
        }

        var entity = new InboxMessage
        {
            Source = source,
            ExternalEventId = externalEventId,
            EventType = eventType,
            Payload = payload,
            Status = "received",
            ReceivedUtc = DateTime.UtcNow,
        };

        try
        {
            await _repo.InsertAsync(entity, publishEvent: false);
            return new InboxRecordResult(entity.Id, AlreadyProcessed: false, null);
        }
        catch (DbUpdateException)
        {
            // Race condition: iki istek aynı anda. Unique constraint atılır.
            // Diğeri kazandı, biz duplicate'ız.
            var racer = await _repo.Table
                .FirstOrDefaultAsync(m => m.Source == source && m.ExternalEventId == externalEventId);
            return new InboxRecordResult(
                racer?.Id ?? 0,
                AlreadyProcessed: true,
                racer);
        }
    }

    public virtual async Task MarkProcessedAsync(long id)
    {
        await _repo.Table.Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, "processed")
                .SetProperty(m => m.ProcessedUtc, DateTime.UtcNow));
    }

    public virtual async Task MarkFailedAsync(long id, string error)
    {
        // İfade ağacı range operatörü ([..]) içeremez — lambda öncesi hesapla.
        var truncatedError = error.Length > 2000 ? error.Substring(0, 2000) : error;
        await _repo.Table.Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, "failed")
                .SetProperty(m => m.Error, truncatedError)
                .SetProperty(m => m.ProcessedUtc, DateTime.UtcNow));
    }
}
