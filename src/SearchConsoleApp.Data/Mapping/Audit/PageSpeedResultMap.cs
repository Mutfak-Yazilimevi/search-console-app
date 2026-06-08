using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class PageSpeedResultMap : IEntityTypeConfiguration<PageSpeedResult>
{
    public void Configure(EntityTypeBuilder<PageSpeedResult> builder)
    {
        builder.ToTable("PageSpeedResult");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.Lcp).HasMaxLength(64);
        builder.Property(x => x.Inp).HasMaxLength(64);
        builder.Property(x => x.Cls).HasMaxLength(64);
        builder.Property(x => x.Strategy).HasMaxLength(16);
        builder.HasIndex(x => x.AuditRunId);
    }
}
