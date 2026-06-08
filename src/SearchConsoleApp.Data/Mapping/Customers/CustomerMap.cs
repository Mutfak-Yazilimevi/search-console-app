using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Customers;

namespace SearchConsoleApp.Data.Mapping.Customers;

/// <summary>
/// ÖRNEK mapping. Her entity için ayrı dosya.
/// Fluent API kullanılır, Data Annotation YASAK.
/// EntityId unique index'i ve ISoftDeletable filter'ı DbContext'te otomatik
/// uygulanır — burada tekrar ekleme.
/// </summary>
public class CustomerMap : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customer");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.Username).HasMaxLength(256);
        builder.Property(c => c.FirstName).HasMaxLength(256);
        builder.Property(c => c.LastName).HasMaxLength(256);
        builder.Property(c => c.PasswordHash).HasMaxLength(512);
        builder.Property(c => c.Roles).IsRequired().HasMaxLength(256).HasDefaultValue("user");
        builder.Property(c => c.TotpSecret).HasMaxLength(64);
        builder.Property(c => c.RecoveryCodesHashes).HasMaxLength(1024);
        builder.Property(c => c.Language).HasMaxLength(8);
        builder.HasIndex(c => c.Email).IsUnique();
    }
}
