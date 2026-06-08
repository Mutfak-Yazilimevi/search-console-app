using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Theming;

namespace SearchConsoleApp.Data.Mapping.Theming;

public class ThemeMap : IEntityTypeConfiguration<Theme>
{
    public void Configure(EntityTypeBuilder<Theme> builder)
    {
        builder.ToTable("Theme");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(128);
        builder.Property(t => t.DisplayName).IsRequired().HasMaxLength(256);
        builder.Property(t => t.Mode).IsRequired().HasMaxLength(16);
        builder.Property(t => t.JsonContent).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
    }
}
