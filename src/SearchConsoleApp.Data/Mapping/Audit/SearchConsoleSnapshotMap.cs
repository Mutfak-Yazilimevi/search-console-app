using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class SearchConsoleSnapshotMap : IEntityTypeConfiguration<SearchConsoleSnapshot>
{
    public void Configure(EntityTypeBuilder<SearchConsoleSnapshot> builder)
    {
        builder.ToTable("SearchConsoleSnapshot");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.PropertyUrl).IsRequired().HasMaxLength(2048);
        builder.Property(s => s.PerformanceJson).IsRequired();
        builder.HasIndex(s => s.AuditRunId).IsUnique();
    }
}
