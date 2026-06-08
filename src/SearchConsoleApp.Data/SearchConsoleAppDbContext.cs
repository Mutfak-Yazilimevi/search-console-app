using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core;

namespace SearchConsoleApp.Data;

/// <summary>
/// Tüm entity mapping'leri çalışan assembly'lerden otomatik yüklenir.
/// `ISoftDeletable` implement eden tüm entity'lere global query filter
/// (`Deleted=false`) otomatik uygulanır.
/// </summary>
public class SearchConsoleAppDbContext : DbContext
{
    public SearchConsoleAppDbContext(DbContextOptions<SearchConsoleAppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Tüm IEntityTypeConfiguration<T> implementasyonlarını yükle
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SearchConsoleAppDbContext).Assembly);

        // ISoftDeletable entity'lere otomatik global query filter
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var deletedProp = Expression.Property(parameter, nameof(ISoftDeletable.Deleted));
                var notDeleted = Expression.Not(deletedProp);
                var lambda = Expression.Lambda(notDeleted, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }

            // EntityId her zaman unique index alır
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex(nameof(BaseEntity.EntityId))
                    .IsUnique();
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
