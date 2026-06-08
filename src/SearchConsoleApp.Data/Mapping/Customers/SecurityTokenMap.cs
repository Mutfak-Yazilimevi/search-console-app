using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Customers;

namespace SearchConsoleApp.Data.Mapping.Customers;

public class SecurityTokenMap : IEntityTypeConfiguration<SecurityToken>
{
    public void Configure(EntityTypeBuilder<SecurityToken> builder)
    {
        builder.ToTable("SecurityToken");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(t => t.Purpose).IsRequired().HasMaxLength(32);
        builder.Property(t => t.CreatedByIp).HasMaxLength(64);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.CustomerId, t.Purpose });
        builder.Ignore(t => t.IsActive);
    }
}
