using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class ContentQualityScoreMap : IEntityTypeConfiguration<ContentQualityScore>
{
    public void Configure(EntityTypeBuilder<ContentQualityScore> builder)
    {
        builder.ToTable("ContentQualityScore");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Url).IsRequired().HasMaxLength(2048);
        builder.Property(c => c.ChecklistJson).IsRequired();
        builder.HasIndex(c => new { c.AuditRunId, c.Url }).IsUnique();
    }
}
