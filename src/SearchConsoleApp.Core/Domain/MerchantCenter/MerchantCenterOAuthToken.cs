using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.MerchantCenter;

/// <summary>
/// Merchant Center API OAuth token (content scope). Separate from Search Console login token.
/// </summary>
public partial class MerchantCenterOAuthToken : BaseEntity
{
    public long CustomerId { get; set; }
    public string EncryptedRefreshToken { get; set; } = "";
    public string Scopes { get; set; } = "";
    public DateTime? AccessTokenExpiresUtc { get; set; }
    public string? EncryptedAccessToken { get; set; }
    public DateTime LinkedAtUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
}
