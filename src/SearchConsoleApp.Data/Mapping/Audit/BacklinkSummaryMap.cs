using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class BacklinkSummaryMap : IEntityTypeConfiguration<BacklinkSummary>
{
    public void Configure(EntityTypeBuilder<BacklinkSummary> builder)
    {
        builder.ToTable("BacklinkSummary");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExternalSource).HasMaxLength(32);
        builder.Property(x => x.ExternalTopDomainsJson).HasMaxLength(4000);
        builder.HasIndex(x => x.AuditRunId);
    }
}
