namespace SearchConsoleApp.Services.Auth;

/// <summary>
/// OAuth provider metadata (endpoint URL'leri, client credentials).
/// appsettings → OAuth:{Provider}:{...}
///
/// Provider tarafından sağlanan stable ID nasıl çıkarılır?
/// - Google: id_token "sub" claim
/// - Microsoft: id_token "oid" veya "sub" claim
/// - GitHub: /user response'unda "id" (integer)
/// - Apple: id_token "sub"
///
/// Email + display name de provider response'undan parse edilir.
/// Her provider için ayrı parser metoda ihtiyaç var çünkü field isimleri farklı.
/// </summary>
public record OAuthProviderConfig(
    string Name,
    string ClientId,
    string ClientSecret,
    string AuthorizeEndpoint,
    string TokenEndpoint,
    string UserInfoEndpoint,
    string Scope,
    string RedirectUri);

/// <summary>Provider'dan parse edilmiş normalized user info.</summary>
public record OAuthUserInfo(
    string ProviderUserId,
    string? Email,
    bool EmailVerified,
    string? DisplayName);
