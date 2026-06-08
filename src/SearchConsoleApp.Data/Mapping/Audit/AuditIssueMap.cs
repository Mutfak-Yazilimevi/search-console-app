using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class AuditIssueMap : IEntityTypeConfiguration<AuditIssue>
{
    public void Configure(EntityTypeBuilder<AuditIssue> builder)
    {
        builder.ToTable("AuditIssue");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.PageUrl).IsRequired().HasMaxLength(2048);
        builder.Property(i => i.RuleId).IsRequired().HasMaxLength(128);
        builder.Property(i => i.Category).IsRequired().HasMaxLength(64);
        builder.Property(i => i.Message).IsRequired().HasMaxLength(1024);
        // Sınırsız JSON/metin — provider'a göre eşlenir (SQL Server: nvarchar(max), SQLite: TEXT).
        builder.Property(i => i.Evidence);
        builder.Property(i => i.FixHint).HasMaxLength(1024);
        builder.Property(i => i.DocUrl).HasMaxLength(512);

        builder.HasIndex(i => i.AuditRunId);
        builder.HasIndex(i => new { i.AuditRunId, i.Severity });
        builder.HasIndex(i => i.RuleId);
    }
}
