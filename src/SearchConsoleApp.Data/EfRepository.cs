using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Auditing;
using SearchConsoleApp.Core.Events;

namespace SearchConsoleApp.Data;

/// <summary>
/// EF Core tabanlı generic repository.
/// - Insert: `EntityId` boşsa Guid v7 atanır.
/// - `ISoftDeletable` entity'ler `DeleteAsync`'de soft delete'e dönüşür.
/// - State değişiminde event publish edilir (publishEvent=true).
/// - Audit notifier varsa entity değişimleri ona da bildirilir.
///
/// IEntityChangeNotifier opsiyonel — DI'da kayıt yoksa skip edilir.
/// ChangeTracker before/after `Audit:CaptureChanges` flag'i ile kontrol edilir.
/// </summary>
public class EfRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    private readonly SearchConsoleAppDbContext _context;
    private readonly IEventPublisher _eventPublisher;
    private readonly IEntityChangeNotifier? _auditNotifier;
    private readonly bool _captureChanges;

    private DbSet<TEntity> Entities => _context.Set<TEntity>();

    public EfRepository(
        SearchConsoleAppDbContext context,
        IEventPublisher eventPublisher,
        IConfiguration config,
        IEntityChangeNotifier? auditNotifier = null)
    {
        _context = context;
        _eventPublisher = eventPublisher;
        _auditNotifier = auditNotifier;
        _captureChanges = config.GetValue("Audit:CaptureChanges", true);
    }

    public IQueryable<TEntity> Table => Entities.AsQueryable();

    public virtual async Task<TEntity?> GetByIdAsync(long id)
    {
        if (id == 0) return null;
        return await Entities.FindAsync(id);
    }

    public virtual async Task<TEntity?> GetByEntityIdAsync(Guid entityId)
    {
        if (entityId == Guid.Empty) return null;
        return await Table.FirstOrDefaultAsync(e => e.EntityId == entityId);
    }

    public virtual async Task<IList<TEntity>> GetAllAsync(
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? func = null)
    {
        var query = Table;
        if (func != null) query = func(query);
        return await query.ToListAsync();
    }

    public virtual async Task InsertAsync(TEntity entity, bool publishEvent = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity.EntityId == Guid.Empty) entity.EntityId = Guid.CreateVersion7();
        await Entities.AddAsync(entity);
        await _context.SaveChangesAsync();

        if (publishEvent)
        {
            await _eventPublisher.PublishAsync(new EntityInsertedEvent<TEntity>(entity));
            if (_auditNotifier != null)
                await _auditNotifier.NotifyAsync(EntityChangeType.Inserted, entity, null);
        }
    }

    public virtual async Task UpdateAsync(TEntity entity, bool publishEvent = true)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Before/after snapshot — Update'ten ÖNCE yakalanır
        IReadOnlyDictionary<string, (object? Old, object? New)>? changes = null;
        if (publishEvent && _captureChanges && _auditNotifier != null)
        {
            changes = CaptureChanges(entity);
        }

        Entities.Update(entity);
        await _context.SaveChangesAsync();

        if (publishEvent)
        {
            await _eventPublisher.PublishAsync(new EntityUpdatedEvent<TEntity>(entity));
            if (_auditNotifier != null)
                await _auditNotifier.NotifyAsync(EntityChangeType.Updated, entity, changes);
        }
    }

    public virtual async Task DeleteAsync(TEntity entity, bool publishEvent = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity is ISoftDeletable soft)
        {
            soft.Deleted = true;
            Entities.Update(entity);
        }
        else
        {
            Entities.Remove(entity);
        }
        await _context.SaveChangesAsync();

        if (publishEvent)
        {
            await _eventPublisher.PublishAsync(new EntityDeletedEvent<TEntity>(entity));
            if (_auditNotifier != null)
                await _auditNotifier.NotifyAsync(EntityChangeType.Deleted, entity, null);
        }
    }

    public virtual async Task HardDeleteAsync(TEntity entity, bool publishEvent = true)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Entities.Remove(entity);
        await _context.SaveChangesAsync();

        if (publishEvent)
        {
            await _eventPublisher.PublishAsync(new EntityDeletedEvent<TEntity>(entity));
            if (_auditNotifier != null)
                await _auditNotifier.NotifyAsync(EntityChangeType.Deleted, entity, null);
        }
    }

    private IReadOnlyDictionary<string, (object? Old, object? New)>? CaptureChanges(TEntity entity)
    {
        var entry = _context.Entry(entity);
        if (entry.State != EntityState.Modified) return null;

        var result = new Dictionary<string, (object? Old, object? New)>();
        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified) continue;
            if (Equals(prop.OriginalValue, prop.CurrentValue)) continue;
            result[prop.Metadata.Name] = (prop.OriginalValue, prop.CurrentValue);
        }
        return result.Count > 0 ? result : null;
    }
}
