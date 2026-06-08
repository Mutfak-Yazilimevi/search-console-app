using SearchConsoleApp.Core;

namespace SearchConsoleApp.Data;

/// <summary>
/// Generic repository. Entity başına ayrı repository yazılmaz.
/// Karmaşık sorgular service katmanında `Table` üzerinden LINQ ile yazılır.
///
/// Soft delete: `ISoftDeletable` entity'ler için DbContext global query filter
/// `Deleted=false` koşulunu otomatik uygular. Silinmişleri görmek için
/// `Table.IgnoreQueryFilters()` kullan.
/// </summary>
public interface IRepository<TEntity> where TEntity : BaseEntity
{
    Task<TEntity?> GetByIdAsync(long id);
    Task<TEntity?> GetByEntityIdAsync(Guid entityId);
    Task<IList<TEntity>> GetAllAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>>? func = null);
    Task InsertAsync(TEntity entity, bool publishEvent = true);
    Task UpdateAsync(TEntity entity, bool publishEvent = true);

    /// <summary>
    /// Soft delete: `ISoftDeletable` ise `Deleted=true` yapar, değilse hard delete eder.
    /// </summary>
    Task DeleteAsync(TEntity entity, bool publishEvent = true);

    /// <summary>Hard delete — soft delete edilebilir entity için bile fiziksel siler.</summary>
    Task HardDeleteAsync(TEntity entity, bool publishEvent = true);

    /// <summary>Raw IQueryable. Karmaşık sorgular için service'lerde kullan.</summary>
    IQueryable<TEntity> Table { get; }
}
