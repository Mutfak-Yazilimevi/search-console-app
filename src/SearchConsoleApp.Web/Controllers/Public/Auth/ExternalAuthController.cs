using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SearchConsoleApp.Services.Auth;
using SearchConsoleApp.Web.Controllers.Public.Auth;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;
using SearchConsoleApp.Web.Framework.RateLimiting;

namespace SearchConsoleApp.Web.Controllers.Public.Auth;

public record ExternalCallbackRequest(string Code, string State);

public record ExternalAuthorizeResponse(string AuthorizeUrl);

/// <summary>
/// OAuth/Social login (Google, Microsoft, GitHub).
/// Route: /api/v1/public/auth/external/*
///
/// Akış:
/// 1. Frontend → GET /external/{provider}?returnUrl=...
///    → { authorizeUrl } döner; frontend buraya redirect eder
/// 2. Provider'da kullanıcı giriş yapar → frontend'in callback URL'ine code ile döner
/// 3. Frontend → POST /external/callback { provider, code, state }
///    → AuthTokens döner
///
/// State CSRF koruması yapar (5 dakikalık in-memory cache).
/// </summary>
[EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
[Route("api/v{version:apiVersion}/public/auth/external")]
public class ExternalAuthController : PublicApiController
{
    private readonly IExternalAuthService _externalAuth;

    public ExternalAuthController(IExternalAuthService externalAuth)
        => _externalAuth = externalAuth;

    /// <summary>Provider'ın authorize URL'ini build et.</summary>
    [HttpGet("{provider}")]
    public async Task<IActionResult> Authorize(string provider, [FromQuery] string? returnUrl = null)
    {
        try
        {
            var url = await _externalAuth.BuildAuthorizeUrlAsync(provider, returnUrl ?? "/");
            return Ok(new ExternalAuthorizeResponse(url));
        }
        catch (NotSupportedException)
        {
            return Problem(statusCode: 400, title: $"Desteklenmeyen provider: {provider}");
        }
        catch (OAuthConfigurationException ex)
        {
            return OAuthProblemResults.FromGuide(ex.Guide);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: 500, title: ex.Message);
        }
    }

    /// <summary>Provider'dan dönen code'u token'a çevir.</summary>
    [HttpPost("{provider}/callback")]
    [Audit("auth.external_login")]
    public async Task<IActionResult> Callback(string provider, [FromBody] ExternalCallbackRequest req)
    {
        var (ip, ua) = GetClientInfo();
        try
        {
            var result = await _externalAuth.HandleCallbackAsync(provider, req.Code, req.State, ip, ua);
            return Ok(ToTokens(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(statusCode: 401, title: ex.Message);
        }
    }

    private static AuthTokens ToTokens(AuthResult r) => new(
        r.AccessToken!, r.AccessTokenExpiresAt!.Value,
        r.RefreshToken!, r.RefreshTokenExpiresAt!.Value,
        new UserInfo(r.Customer!.EntityId, r.Customer.Email,
            r.Customer.FirstName, r.Customer.LastName,
            r.Customer.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToList()));

    private (string? Ip, string? UserAgent) GetClientInfo()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        return (ip, string.IsNullOrWhiteSpace(ua) ? null : ua);
    }
}

/// <summary>
/// Login halindeyken bağlı OAuth provider'ları yönet.
/// Route: /api/v1/web/account/external/*
/// </summary>
public class WebExternalLoginController : WebApiController
{
    private readonly IExternalAuthService _externalAuth;
    private readonly Core.RequestScope.IRequestScope _scope;

    public WebExternalLoginController(IExternalAuthService externalAuth, Core.RequestScope.IRequestScope scope)
    {
        _externalAuth = externalAuth;
        _scope = scope;
    }

    [HttpGet("external")]
    public async Task<IActionResult> List()
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();
        var providers = await _externalAuth.GetLinkedProvidersAsync(customerId);
        return Ok(providers.Select(p => new
        {
            p.Provider,
            p.Email,
            p.DisplayName,
            p.LinkedOnUtc,
            p.LastLoginUtc,
        }));
    }

    [HttpPost("external/{provider}/link")]
    [Audit("account.external_link")]
    public async Task<IActionResult> Link(string provider, [FromBody] ExternalCallbackRequest req)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();
        try
        {
            await _externalAuth.LinkProviderAsync(customerId, provider, req.Code, req.State);
            return Ok(new { ok = true });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: 409, title: ex.Message);
        }
    }

    [HttpDelete("external/{provider}")]
    [Audit("account.external_unlink")]
    public async Task<IActionResult> Unlink(string provider)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();
        try
        {
            await _externalAuth.UnlinkProviderAsync(customerId, provider);
            return Ok(new { ok = true });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: 400, title: ex.Message);
        }
    }
}
