using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Identity;

namespace SearchConsoleApp.Data.Mapping.Identity;

public class DeviceSessionMap : IEntityTypeConfiguration<DeviceSession>
{
    public void Configure(EntityTypeBuilder<DeviceSession> builder)
    {
        builder.ToTable("DeviceSession");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Audience).IsRequired().HasMaxLength(16);
        builder.Property(s => s.RefreshTokenHash).HasMaxLength(128);
        builder.Property(s => s.IpAddress).HasMaxLength(64);
        builder.Property(s => s.IpCountry).HasMaxLength(8);
        builder.Property(s => s.IpCity).HasMaxLength(128);
        builder.Property(s => s.UserAgent).HasMaxLength(512);
        builder.Property(s => s.RevokedReason).HasMaxLength(32);

        builder.Ignore(s => s.IsActive);

        builder.HasIndex(s => s.CustomerId);
        builder.HasIndex(s => s.DeviceId);
        builder.HasIndex(s => s.RefreshTokenHash);
        builder.HasIndex(s => s.StartedUtc);
    }
}
