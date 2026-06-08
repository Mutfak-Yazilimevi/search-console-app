using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SearchConsoleApp.Core.Domain.Audit;

namespace SearchConsoleApp.Data.Mapping.Audit;

public class GoogleOAuthTokenMap : IEntityTypeConfiguration<GoogleOAuthToken>
{
    public void Configure(EntityTypeBuilder<GoogleOAuthToken> builder)
    {
        builder.ToTable("GoogleOAuthToken");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.EncryptedRefreshToken).IsRequired();
        builder.Property(t => t.Scopes).HasMaxLength(512);
        builder.HasIndex(t => t.CustomerId).IsUnique();
    }
}
