using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Outbox;

namespace SearchConsoleApp.Data.Mapping.Outbox;

public class OutboxMessageMap : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessage");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MessageType).IsRequired().HasMaxLength(128);
        builder.Property(m => m.Target).IsRequired().HasMaxLength(512);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.HeadersJson);
        builder.Property(m => m.LastError).HasMaxLength(2048);
        builder.Property(m => m.Status).IsRequired().HasMaxLength(16).HasDefaultValue("pending");
        builder.Property(m => m.Audience).IsRequired().HasMaxLength(16);
        builder.Property(m => m.CorrelationId).HasMaxLength(128);

        // Dispatcher polling pattern: WHERE Status='pending' AND AvailableAtUtc <= now
        // ORDER BY CreatedOnUtc.
        // Composite index Status'a göre filtreleyip Available zamanına bakar.
        builder.HasIndex(m => new { m.Status, m.AvailableAtUtc });
        builder.HasIndex(m => m.CreatedOnUtc);
    }
}
