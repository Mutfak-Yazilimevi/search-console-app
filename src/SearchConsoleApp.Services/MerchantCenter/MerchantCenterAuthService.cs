using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Auth;
using SearchConsoleApp.Services.Security;

namespace SearchConsoleApp.Services.MerchantCenter;

public interface IMerchantCenterAuthService
{
    Task<string> BuildAuthorizeUrlAsync(long customerId, string returnUrl);
    Task HandleCallbackAsync(long customerId, string code, string state);
    Task<bool> IsConnectedAsync(long customerId);
    Task DisconnectAsync(long customerId);
    Task<string?> GetAccessTokenAsync(long customerId, CancellationToken cancellationToken = default);
}

public partial class MerchantCenterAuthService : IMerchantCenterAuthService, IScopedService
{
    private const string Scope = "https://www.googleapis.com/auth/content";

    private readonly IRepository<MerchantCenterOAuthToken> _tokenRepo;
    private readonly ITokenProtector _protector;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MerchantCenterAuthService> _logger;

    public MerchantCenterAuthService(
        IRepository<MerchantCenterOAuthToken> tokenRepo,
        ITokenProtector protector,
        IConfiguration config,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<MerchantCenterAuthService> logger)
    {
        _tokenRepo = tokenRepo;
        _protector = protector;
        _config = config;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<string> BuildAuthorizeUrlAsync(long customerId, string returnUrl)
    {
        var section = GetConfig();
        var state = GenerateState();
        _cache.Set($"gmc-oauth:{state}", new StateEntry(customerId, returnUrl), TimeSpan.FromMinutes(10));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = section.ClientId,
            ["redirect_uri"] = section.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["state"] = state,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
        };

        var qs = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return Task.FromResult($"https://accounts.google.com/o/oauth2/v2/auth?{qs}");
    }

    public async Task HandleCallbackAsync(long customerId, string code, string state)
    {
        if (!_cache.TryGetValue<StateEntry>($"gmc-oauth:{state}", out var entry) || entry == null)
            throw new UnauthorizedAccessException("Invalid or expired OAuth state.");

        if (entry.CustomerId != customerId)
            throw new UnauthorizedAccessException("State customer mismatch.");

        _cache.Remove($"gmc-oauth:{state}");

        var section = GetConfig();
        var http = _httpClientFactory.CreateClient();
        var tokenReq = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = section.ClientId,
            ["client_secret"] = section.ClientSecret,
            ["redirect_uri"] = section.RedirectUri,
        });

        var tokenRes = await http.PostAsync("https://oauth2.googleapis.com/token", tokenReq);
        if (!tokenRes.IsSuccessStatusCode)
        {
            var body = await tokenRes.Content.ReadAsStringAsync();
            _logger.LogWarning("GMC token exchange failed: {Status} {Body}", tokenRes.StatusCode, body);
            throw new UnauthorizedAccessException("Failed to exchange authorization code.");
        }

        var tokenJson = await tokenRes.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = tokenJson.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        if (string.IsNullOrEmpty(refreshToken))
            throw new UnauthorizedAccessException("Google did not return a refresh token. Revoke app access and retry.");

        var accessToken = tokenJson.GetProperty("access_token").GetString()!;
        var expiresIn = tokenJson.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        var existing = await _tokenRepo.Table.FirstOrDefaultAsync(t => t.CustomerId == customerId);
        if (existing == null)
        {
            await _tokenRepo.InsertAsync(new MerchantCenterOAuthToken
            {
                CustomerId = customerId,
                EncryptedRefreshToken = _protector.Protect(refreshToken),
                EncryptedAccessToken = _protector.Protect(accessToken),
                AccessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60),
                Scopes = Scope,
                LinkedAtUtc = DateTime.UtcNow,
            }, publishEvent: false);
        }
        else
        {
            existing.EncryptedRefreshToken = _protector.Protect(refreshToken);
            existing.EncryptedAccessToken = _protector.Protect(accessToken);
            existing.AccessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60);
            existing.Scopes = Scope;
            existing.LinkedAtUtc = DateTime.UtcNow;
            await _tokenRepo.UpdateAsync(existing, publishEvent: false);
        }
    }

    public async Task<bool> IsConnectedAsync(long customerId)
        => await _tokenRepo.Table.AnyAsync(t => t.CustomerId == customerId);

    public async Task DisconnectAsync(long customerId)
    {
        var token = await _tokenRepo.Table.FirstOrDefaultAsync(t => t.CustomerId == customerId);
        if (token != null) await _tokenRepo.HardDeleteAsync(token);
    }

    public async Task<string?> GetAccessTokenAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var token = await _tokenRepo.Table.FirstOrDefaultAsync(t => t.CustomerId == customerId, cancellationToken);
        if (token == null) return null;

        if (token.AccessTokenExpiresUtc > DateTime.UtcNow && !string.IsNullOrEmpty(token.EncryptedAccessToken))
        {
            token.LastUsedUtc = DateTime.UtcNow;
            await _tokenRepo.UpdateAsync(token, publishEvent: false);
            return _protector.Unprotect(token.EncryptedAccessToken);
        }

        return await RefreshAccessTokenAsync(token, cancellationToken);
    }

    private async Task<string?> RefreshAccessTokenAsync(MerchantCenterOAuthToken token, CancellationToken cancellationToken)
    {
        var section = GetConfig();
        var refreshToken = _protector.Unprotect(token.EncryptedRefreshToken);
        var http = _httpClientFactory.CreateClient();

        var tokenReq = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = section.ClientId,
            ["client_secret"] = section.ClientSecret,
        });

        var tokenRes = await http.PostAsync("https://oauth2.googleapis.com/token", tokenReq, cancellationToken);
        if (!tokenRes.IsSuccessStatusCode)
        {
            _logger.LogWarning("GMC token refresh failed for customer {CustomerId}", token.CustomerId);
            return null;
        }

        var tokenJson = await tokenRes.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var accessToken = tokenJson.GetProperty("access_token").GetString()!;
        var expiresIn = tokenJson.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        token.EncryptedAccessToken = _protector.Protect(accessToken);
        token.AccessTokenExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        token.LastUsedUtc = DateTime.UtcNow;
        await _tokenRepo.UpdateAsync(token, publishEvent: false);
        return accessToken;
    }

    private (string ClientId, string ClientSecret, string RedirectUri) GetConfig()
    {
        var section = _config.GetSection("Google:MerchantCenter");
        var clientId = section["ClientId"] ?? _config["Google:MerchantCenter:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            clientId = _config["OAuth:google:ClientId"] ?? "";
        if (string.IsNullOrWhiteSpace(clientId))
            throw new OAuthConfigurationException(OAuthSetupGuides.ForGoogleMerchantCenter(_config));

        var clientSecret = section["ClientSecret"] ?? _config["OAuth:google:ClientSecret"] ?? "";
        var redirectUri = section["RedirectUri"]
            ?? "http://localhost:4200/auth/merchant-center/callback";
        return (clientId, clientSecret, redirectUri);
    }

    private static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private record StateEntry(long CustomerId, string ReturnUrl);
}
