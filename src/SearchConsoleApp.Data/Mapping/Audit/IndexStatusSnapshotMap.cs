using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class IndexStatusSnapshotMap : IEntityTypeConfiguration<IndexStatusSnapshot>
{
    public void Configure(EntityTypeBuilder<IndexStatusSnapshot> builder)
    {
        builder.ToTable("IndexStatusSnapshot");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Domain).IsRequired().HasMaxLength(512);
        builder.Property(x => x.Source).HasMaxLength(32);
        builder.HasIndex(x => x.AuditRunId);
    }
}
