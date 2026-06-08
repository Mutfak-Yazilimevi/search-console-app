using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Inbox;

namespace SearchConsoleApp.Data.Mapping.Inbox;

public class InboxMessageMap : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessage");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Source).IsRequired().HasMaxLength(64);
        builder.Property(m => m.ExternalEventId).IsRequired().HasMaxLength(256);
        builder.Property(m => m.EventType).IsRequired().HasMaxLength(128);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.Status).IsRequired().HasMaxLength(16);
        builder.Property(m => m.Error).HasMaxLength(2048);

        // Idempotency anahtarı: aynı source'tan aynı event sadece bir kez kaydedilir
        builder.HasIndex(m => new { m.Source, m.ExternalEventId }).IsUnique();
        builder.HasIndex(m => m.Status);
    }
}
