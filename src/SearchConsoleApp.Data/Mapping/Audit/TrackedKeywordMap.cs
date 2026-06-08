using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class TrackedKeywordMap : IEntityTypeConfiguration<TrackedKeyword>
{
    public void Configure(EntityTypeBuilder<TrackedKeyword> builder)
    {
        builder.ToTable("TrackedKeyword");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Keyword).IsRequired().HasMaxLength(512);
        builder.HasIndex(x => x.AuditRunId);
    }
}
