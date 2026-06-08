using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Audit;

/// <summary>
/// Google Search Console API refresh token (encrypted at rest).
/// Separate from login OAuth — stores webmasters.readonly scope.
/// </summary>
public partial class GoogleOAuthToken : BaseEntity
{
    public long CustomerId { get; set; }
    public string EncryptedRefreshToken { get; set; } = "";
    public string Scopes { get; set; } = "";
    public DateTime? AccessTokenExpiresUtc { get; set; }
    public string? EncryptedAccessToken { get; set; }
    public DateTime LinkedAtUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
}
