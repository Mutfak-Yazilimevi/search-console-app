using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.PriceBenchmark;

namespace SearchConsoleApp.Data.Mapping.PriceBenchmark;

public class PriceBenchmarkRunMap : IEntityTypeConfiguration<PriceBenchmarkRun>
{
    public void Configure(EntityTypeBuilder<PriceBenchmarkRun> builder)
    {
        builder.ToTable("PriceBenchmarkRun");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InputUrl).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.NormalizedUrl).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2048);
        builder.Property(x => x.ProgressPhase).HasMaxLength(64);
        builder.Property(x => x.ProgressMessage).HasMaxLength(512);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.NormalizedUrl);
    }
}

public class PriceBenchmarkItemMap : IEntityTypeConfiguration<PriceBenchmarkItem>
{
    public void Configure(EntityTypeBuilder<PriceBenchmarkItem> builder)
    {
        builder.ToTable("PriceBenchmarkItem");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PageUrl).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.Title).HasMaxLength(512);
        builder.Property(x => x.PriceCurrency).HasMaxLength(8);
        builder.Property(x => x.MinOfferLink).HasMaxLength(2048);
        builder.Property(x => x.MinOfferSource).HasMaxLength(256);
        builder.Property(x => x.MaxOfferLink).HasMaxLength(2048);
        builder.Property(x => x.MaxOfferSource).HasMaxLength(256);
        builder.Property(x => x.ShoppingError).HasMaxLength(1024);
        builder.Property(x => x.OurPrice).HasPrecision(18, 2);
        builder.Property(x => x.MinMarketPrice).HasPrecision(18, 2);
        builder.Property(x => x.MaxMarketPrice).HasPrecision(18, 2);
        builder.Property(x => x.WeightedAvgMarketPrice).HasPrecision(18, 2);
        builder.Property(x => x.DeltaPercent).HasPrecision(9, 2);
        builder.HasIndex(x => x.RunId);
        builder.HasIndex(x => new { x.RunId, x.PageUrl }).IsUnique();
    }
}
