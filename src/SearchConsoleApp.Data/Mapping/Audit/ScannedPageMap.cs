using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class ScannedPageMap : IEntityTypeConfiguration<ScannedPage>
{
    public void Configure(EntityTypeBuilder<ScannedPage> builder)
    {
        builder.ToTable("ScannedPage");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Url).IsRequired().HasMaxLength(2048);
        builder.Property(p => p.Title).HasMaxLength(512);
        builder.Property(p => p.MetaDescription).HasMaxLength(1024);

        builder.HasIndex(p => p.AuditRunId);
        builder.HasIndex(p => new { p.AuditRunId, p.Url });
    }
}
