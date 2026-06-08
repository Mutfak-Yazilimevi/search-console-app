using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Auditing;

namespace SearchConsoleApp.Data.Mapping.Auditing;

public class AuditLogArchiveMap : IEntityTypeConfiguration<AuditLogArchive>
{
    public void Configure(EntityTypeBuilder<AuditLogArchive> builder)
    {
        builder.ToTable("AuditLogArchive");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Audience).IsRequired().HasMaxLength(16);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(128);
        builder.Property(a => a.ActorEmail).HasMaxLength(256);
        builder.Property(a => a.ActorIp).HasMaxLength(64);
        builder.Property(a => a.ActorUserAgent).HasMaxLength(512);
        builder.Property(a => a.TargetType).HasMaxLength(128);
        builder.Property(a => a.Outcome).IsRequired().HasMaxLength(16);
        builder.Property(a => a.FailureReason).HasMaxLength(256);
        builder.Property(a => a.CorrelationId).HasMaxLength(128);
        builder.Property(a => a.ChangesJson);
        builder.Property(a => a.MetadataJson);

        // Aktif tablodaki ile aynı sorgu pattern'leri
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.ActorCustomerId);
        builder.HasIndex(a => new { a.TargetType, a.TargetId });
        builder.HasIndex(a => a.Action);
        builder.HasIndex(a => a.OriginalId);
    }
}
