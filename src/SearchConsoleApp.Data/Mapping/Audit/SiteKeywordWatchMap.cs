using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class SiteKeywordWatchMap : IEntityTypeConfiguration<SiteKeywordWatch>
{
    public void Configure(EntityTypeBuilder<SiteKeywordWatch> builder)
    {
        builder.ToTable("SiteKeywordWatch");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SiteHost).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Keyword).IsRequired().HasMaxLength(512);
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => new { x.CustomerId, x.SiteHost, x.Keyword }).IsUnique();
    }
}
