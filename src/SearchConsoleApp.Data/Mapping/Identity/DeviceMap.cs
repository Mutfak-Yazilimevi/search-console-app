using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Identity;

namespace SearchConsoleApp.Data.Mapping.Identity;

public class DeviceMap : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Device");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Fingerprint).IsRequired().HasMaxLength(128);
        builder.Property(d => d.Name).HasMaxLength(256);
        builder.Property(d => d.DeviceType).IsRequired().HasMaxLength(32);
        builder.Property(d => d.FirstUserAgent).HasMaxLength(512);

        // Fingerprint per customer benzersiz — aynı kullanıcı + aynı cihaz = aynı kayıt
        builder.HasIndex(d => new { d.CustomerId, d.Fingerprint }).IsUnique();
        builder.HasIndex(d => d.CustomerId);
    }
}
