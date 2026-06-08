using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Customers;

namespace SearchConsoleApp.Data.Mapping.Customers;

public class ExternalLoginMap : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("ExternalLogin");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Provider).IsRequired().HasMaxLength(32);
        builder.Property(e => e.ProviderUserId).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Email).HasMaxLength(256);
        builder.Property(e => e.DisplayName).HasMaxLength(256);

        // Provider + ProviderUserId benzersiz — aynı Google user iki customer'a bağlanamaz
        builder.HasIndex(e => new { e.Provider, e.ProviderUserId }).IsUnique();
        builder.HasIndex(e => e.CustomerId);
    }
}
