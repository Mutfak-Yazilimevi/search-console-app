using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class AuditRunMap : IEntityTypeConfiguration<AuditRun>
{
    public void Configure(EntityTypeBuilder<AuditRun> builder)
    {
        builder.ToTable("AuditRun");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.InputUrl).IsRequired().HasMaxLength(2048);
        builder.Property(r => r.NormalizedUrl).IsRequired().HasMaxLength(2048);
        builder.Property(r => r.Status).IsRequired();
        builder.Property(r => r.Mode).IsRequired();
        builder.Property(r => r.ErrorMessage).HasMaxLength(2048);
        builder.Property(r => r.ProgressPhase).HasMaxLength(64);
        builder.Property(r => r.ProgressMessage).HasMaxLength(512);
        builder.Property(r => r.SearchConsolePropertyUrl).HasMaxLength(2048);

        builder.HasIndex(r => r.CustomerId);
        builder.HasIndex(r => r.ScheduledAuditId);
        builder.HasIndex(r => r.CreatedAt);
        builder.HasIndex(r => r.NormalizedUrl);
    }
}
