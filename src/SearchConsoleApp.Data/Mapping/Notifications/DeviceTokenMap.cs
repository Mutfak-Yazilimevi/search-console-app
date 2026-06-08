using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Notifications;

namespace SearchConsoleApp.Data.Mapping.Notifications;

public class DeviceTokenMap : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("DeviceToken");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Token).IsRequired().HasMaxLength(512);
        builder.Property(t => t.Provider).IsRequired().HasMaxLength(16);
        builder.Property(t => t.Platform).IsRequired().HasMaxLength(16);
        builder.Property(t => t.DeviceName).HasMaxLength(256);
        builder.Property(t => t.AppVersion).HasMaxLength(64);
        builder.HasIndex(t => new { t.CustomerId, t.Token }).IsUnique();
    }
}
