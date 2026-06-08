using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.MerchantCenter;

namespace SearchConsoleApp.Data.Mapping.MerchantCenter;

public class ProductComplianceRunMap : IEntityTypeConfiguration<ProductComplianceRun>
{
    public void Configure(EntityTypeBuilder<ProductComplianceRun> builder)
    {
        builder.ToTable("ProductComplianceRun");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InputUrl).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.NormalizedUrl).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.MerchantCenterAccountId).HasMaxLength(64);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2048);
        builder.Property(x => x.ProgressPhase).HasMaxLength(64);
        builder.Property(x => x.ProgressMessage).HasMaxLength(512);
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.NormalizedUrl);
    }
}

public class ProductComplianceItemMap : IEntityTypeConfiguration<ProductComplianceItem>
{
    public void Configure(EntityTypeBuilder<ProductComplianceItem> builder)
    {
        builder.ToTable("ProductComplianceItem");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PageUrl).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.Title).HasMaxLength(512);
        builder.Property(x => x.OfferId).HasMaxLength(256);
        builder.Property(x => x.GmcStatus).HasMaxLength(64);
        builder.Property(x => x.ExtractedDataJson).IsRequired();
        builder.HasIndex(x => x.RunId);
        builder.HasIndex(x => new { x.RunId, x.PageUrl }).IsUnique();
    }
}

public class ProductComplianceIssueMap : IEntityTypeConfiguration<ProductComplianceIssue>
{
    public void Configure(EntityTypeBuilder<ProductComplianceIssue> builder)
    {
        builder.ToTable("ProductComplianceIssue");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PageUrl).HasMaxLength(2048);
        builder.Property(x => x.RuleId).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Field).HasMaxLength(64);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.FixHint).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.DocUrl).HasMaxLength(512);
        builder.Property(x => x.GmcIssueCode).HasMaxLength(128);
        builder.Property(x => x.Evidence).HasMaxLength(4000);
        builder.HasIndex(x => x.RunId);
        builder.HasIndex(x => x.ItemId);
        builder.HasIndex(x => x.RuleId);
    }
}

public class MerchantCenterOAuthTokenMap : IEntityTypeConfiguration<MerchantCenterOAuthToken>
{
    public void Configure(EntityTypeBuilder<MerchantCenterOAuthToken> builder)
    {
        builder.ToTable("MerchantCenterOAuthToken");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EncryptedRefreshToken).IsRequired();
        builder.Property(x => x.Scopes).HasMaxLength(512);
        builder.HasIndex(x => x.CustomerId).IsUnique();
    }
}
