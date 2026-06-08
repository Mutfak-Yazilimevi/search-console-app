using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class KeywordSerpSnapshotMap : IEntityTypeConfiguration<KeywordSerpSnapshot>
{
    public void Configure(EntityTypeBuilder<KeywordSerpSnapshot> builder)
    {
        builder.ToTable("KeywordSerpSnapshot");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Keyword).IsRequired().HasMaxLength(512);
        builder.Property(x => x.MatchedUrl).HasMaxLength(2048);
        builder.HasIndex(x => x.AuditRunId);
    }
}
