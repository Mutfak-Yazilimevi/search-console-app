using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class ScheduledAuditMap : IEntityTypeConfiguration<ScheduledAudit>
{
    public void Configure(EntityTypeBuilder<ScheduledAudit> builder)
    {
        builder.ToTable("ScheduledAudit");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Label).HasMaxLength(256);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.SearchConsolePropertyUrl).HasMaxLength(2048);
        builder.Property(x => x.MigrationSourceUrl).HasMaxLength(2048);
        builder.Property(x => x.Ga4PropertyId).HasMaxLength(64);
        builder.Property(x => x.WebhookUrl).HasMaxLength(2048);

        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => new { x.IsEnabled, x.NextRunUtc });
    }
}
