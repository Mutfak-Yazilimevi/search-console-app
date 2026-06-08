using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Auth;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Security;

namespace SearchConsoleApp.Services.Auth;

public partial class ExternalAuthService : IExternalAuthService, IScopedService
{
    private readonly IRepository<Customer> _customerRepo;
    private readonly IRepository<ExternalLogin> _externalRepo;
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _stateCache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ExternalAuthService> _logger;

    public ExternalAuthService(
        IRepository<Customer> customerRepo,
        IRepository<ExternalLogin> externalRepo,
        IAuthService authService,
        IConfiguration config,
        IMemoryCache stateCache,
        IHttpClientFactory httpClientFactory,
        IPasswordHasher passwordHasher,
        ILogger<ExternalAuthService> logger)
    {
        _customerRepo = customerRepo;
        _externalRepo = externalRepo;
        _authService = authService;
        _config = config;
        _stateCache = stateCache;
        _httpClientFactory = httpClientFactory;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public Task<string> BuildAuthorizeUrlAsync(string provider, string returnUrl)
    {
        var p = GetProviderConfig(provider);
        var state = GenerateState();

        // state'i cache'le: callback'te provider+returnUrl doğrulanır
        _stateCache.Set($"oauth:state:{state}",
            new StateEntry(provider, returnUrl),
            TimeSpan.FromMinutes(10));

        var query = new Dictionary<string, string>
        {
            ["client_id"] = p.ClientId,
            ["redirect_uri"] = p.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = p.Scope,
            ["state"] = state,
        };

        var qs = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return Task.FromResult($"{p.AuthorizeEndpoint}?{qs}");
    }

    public async Task<AuthResult> HandleCallbackAsync(string provider, string code, string state,
                                                       string? ip, string? userAgent)
    {
        // State doğrula
        if (!_stateCache.TryGetValue<StateEntry>($"oauth:state:{state}", out var stateEntry)
            || stateEntry == null
            || !string.Equals(stateEntry.Provider, provider, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş state.");
        }
        _stateCache.Remove($"oauth:state:{state}");

        var p = GetProviderConfig(provider);
        var userInfo = await ExchangeCodeAndFetchUserAsync(p, code);

        if (string.IsNullOrEmpty(userInfo.ProviderUserId))
            throw new UnauthorizedAccessException($"{provider} provider user ID dönmedi.");

        // Mevcut ExternalLogin var mı? → o customer'a login
        var existing = await _externalRepo.Table
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderUserId == userInfo.ProviderUserId);

        if (existing != null)
        {
            existing.LastLoginUtc = DateTime.UtcNow;
            await _externalRepo.UpdateAsync(existing, publishEvent: false);

            var existingCustomer = await _customerRepo.GetByIdAsync(existing.CustomerId)
                ?? throw new InvalidOperationException("ExternalLogin var ama Customer yok.");

            return await IssueTokensViaAuthServiceAsync(existingCustomer, ip, userAgent);
        }

        // Yeni: email match'i ile mevcut customer'a link et veya yeni customer oluştur
        Customer? customer = null;
        if (!string.IsNullOrEmpty(userInfo.Email) && userInfo.EmailVerified)
        {
            customer = await _customerRepo.Table
                .FirstOrDefaultAsync(c => c.Email == userInfo.Email!.ToLowerInvariant());
        }

        if (customer == null)
        {
            // Yeni customer — provider'dan gelen email + display name ile
            customer = new Customer
            {
                Email = userInfo.Email?.ToLowerInvariant() ?? $"{provider}-{userInfo.ProviderUserId}@external.local",
                FirstName = userInfo.DisplayName,
                LastName = null,
                PasswordHash = null,  // parola YOK — sadece OAuth ile giriş
                EmailConfirmed = userInfo.EmailVerified,
                Active = true,
                Roles = "user",
                CreatedOnUtc = DateTime.UtcNow,
            };
            await _customerRepo.InsertAsync(customer);
        }

        // ExternalLogin kaydı
        await _externalRepo.InsertAsync(new ExternalLogin
        {
            CustomerId = customer.Id,
            Provider = provider,
            ProviderUserId = userInfo.ProviderUserId,
            Email = userInfo.Email,
            DisplayName = userInfo.DisplayName,
            LinkedOnUtc = DateTime.UtcNow,
            LastLoginUtc = DateTime.UtcNow,
        });

        return await IssueTokensViaAuthServiceAsync(customer, ip, userAgent);
    }

    public async Task LinkProviderAsync(long customerId, string provider, string code, string state)
    {
        if (!_stateCache.TryGetValue<StateEntry>($"oauth:state:{state}", out var stateEntry)
            || stateEntry == null
            || !string.Equals(stateEntry.Provider, provider, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Geçersiz state.");
        }
        _stateCache.Remove($"oauth:state:{state}");

        var p = GetProviderConfig(provider);
        var userInfo = await ExchangeCodeAndFetchUserAsync(p, code);

        // Başka bir customer'a zaten bağlı mı?
        var existing = await _externalRepo.Table
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderUserId == userInfo.ProviderUserId);
        if (existing != null && existing.CustomerId != customerId)
        {
            throw new InvalidOperationException($"Bu {provider} hesabı başka bir kullanıcıya bağlı.");
        }
        if (existing != null) return;  // zaten bu customer'a bağlı, idempotent

        await _externalRepo.InsertAsync(new ExternalLogin
        {
            CustomerId = customerId,
            Provider = provider,
            ProviderUserId = userInfo.ProviderUserId,
            Email = userInfo.Email,
            DisplayName = userInfo.DisplayName,
            LinkedOnUtc = DateTime.UtcNow,
        });
    }

    public async Task UnlinkProviderAsync(long customerId, string provider)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException("Customer yok.");

        var link = await _externalRepo.Table
            .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.Provider == provider);
        if (link == null) return;

        // En az bir login yolu kalmalı (parola veya başka provider)
        var otherProviders = await _externalRepo.Table
            .CountAsync(x => x.CustomerId == customerId && x.Provider != provider);
        if (string.IsNullOrEmpty(customer.PasswordHash) && otherProviders == 0)
        {
            throw new InvalidOperationException(
                "Son giriş yolunu kaldıramazsın. Önce parola belirle veya başka provider bağla.");
        }

        await _externalRepo.HardDeleteAsync(link);
    }

    public Task<IList<ExternalLogin>> GetLinkedProvidersAsync(long customerId)
        => _externalRepo.GetAllAsync(q => q.Where(x => x.CustomerId == customerId));

    // === Internal helpers ===

    private OAuthProviderConfig GetProviderConfig(string provider)
    {
        var section = _config.GetSection($"OAuth:{provider}");
        var clientId = section["ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
                throw new OAuthConfigurationException(OAuthSetupGuides.ForGoogleLogin(_config));
            throw new InvalidOperationException(
                $"OAuth:{provider}:ClientId yapılandırılmamış.");
        }

        var redirectUri = section["RedirectUri"];
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
                throw new OAuthConfigurationException(OAuthSetupGuides.ForGoogleLogin(_config));
            throw new InvalidOperationException($"OAuth:{provider}:RedirectUri yapılandırılmamış.");
        }

        // Provider-specific defaults — appsettings override edebilir
        return provider.ToLowerInvariant() switch
        {
            "google" => new OAuthProviderConfig(
                Name: "google",
                ClientId: clientId,
                ClientSecret: section["ClientSecret"] ?? "",
                AuthorizeEndpoint: section["AuthorizeEndpoint"] ?? "https://accounts.google.com/o/oauth2/v2/auth",
                TokenEndpoint: section["TokenEndpoint"] ?? "https://oauth2.googleapis.com/token",
                UserInfoEndpoint: section["UserInfoEndpoint"] ?? "https://openidconnect.googleapis.com/v1/userinfo",
                Scope: section["Scope"] ?? "openid email profile",
                RedirectUri: redirectUri),

            "microsoft" => new OAuthProviderConfig(
                Name: "microsoft",
                ClientId: clientId,
                ClientSecret: section["ClientSecret"] ?? "",
                AuthorizeEndpoint: section["AuthorizeEndpoint"] ?? "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                TokenEndpoint: section["TokenEndpoint"] ?? "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                UserInfoEndpoint: section["UserInfoEndpoint"] ?? "https://graph.microsoft.com/oidc/userinfo",
                Scope: section["Scope"] ?? "openid email profile",
                RedirectUri: redirectUri),

            "github" => new OAuthProviderConfig(
                Name: "github",
                ClientId: clientId,
                ClientSecret: section["ClientSecret"] ?? "",
                AuthorizeEndpoint: section["AuthorizeEndpoint"] ?? "https://github.com/login/oauth/authorize",
                TokenEndpoint: section["TokenEndpoint"] ?? "https://github.com/login/oauth/access_token",
                UserInfoEndpoint: section["UserInfoEndpoint"] ?? "https://api.github.com/user",
                Scope: section["Scope"] ?? "read:user user:email",
                RedirectUri: redirectUri),

            _ => throw new NotSupportedException($"OAuth provider desteklenmiyor: {provider}")
        };
    }

    private async Task<OAuthUserInfo> ExchangeCodeAndFetchUserAsync(OAuthProviderConfig p, string code)
    {
        var http = _httpClientFactory.CreateClient();

        // 1. Code → access_token
        var tokenReq = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = p.ClientId,
            ["client_secret"] = p.ClientSecret,
            ["redirect_uri"] = p.RedirectUri,
        });

        using var tokenReqMsg = new HttpRequestMessage(HttpMethod.Post, p.TokenEndpoint)
        {
            Content = tokenReq
        };
        tokenReqMsg.Headers.Accept.Add(new("application/json"));   // GitHub için zorunlu

        var tokenRes = await http.SendAsync(tokenReqMsg);
        if (!tokenRes.IsSuccessStatusCode)
        {
            var body = await tokenRes.Content.ReadAsStringAsync();
            _logger.LogWarning("OAuth token endpoint hata: {Status} {Body}", tokenRes.StatusCode, body);
            throw new UnauthorizedAccessException("OAuth token alınamadı.");
        }

        var tokenJson = await tokenRes.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokenJson.GetProperty("access_token").GetString()
            ?? throw new UnauthorizedAccessException("access_token yok.");

        // 2. UserInfo endpoint çağır
        using var userReq = new HttpRequestMessage(HttpMethod.Get, p.UserInfoEndpoint);
        userReq.Headers.Authorization = new("Bearer", accessToken);
        userReq.Headers.UserAgent.ParseAdd("SearchConsoleApp/1.0");   // GitHub User-Agent zorunlu

        var userRes = await http.SendAsync(userReq);
        if (!userRes.IsSuccessStatusCode)
            throw new UnauthorizedAccessException("UserInfo alınamadı.");

        var userJson = await userRes.Content.ReadFromJsonAsync<JsonElement>();
        return ParseUserInfo(p.Name, userJson);
    }

    /// <summary>
    /// Provider-specific user info parse. Her provider farklı field isimleri kullanır.
    /// </summary>
    private static OAuthUserInfo ParseUserInfo(string provider, JsonElement json)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => new OAuthUserInfo(
                ProviderUserId: json.GetProperty("sub").GetString()!,
                Email: GetStringOrNull(json, "email"),
                EmailVerified: json.TryGetProperty("email_verified", out var ev) && ev.GetBoolean(),
                DisplayName: GetStringOrNull(json, "name")),

            "microsoft" => new OAuthUserInfo(
                ProviderUserId: GetStringOrNull(json, "sub") ?? GetStringOrNull(json, "oid")!,
                Email: GetStringOrNull(json, "email"),
                EmailVerified: true,  // MS tenant email'leri genelde verified
                DisplayName: GetStringOrNull(json, "name")),

            "github" => new OAuthUserInfo(
                ProviderUserId: json.GetProperty("id").GetRawText(),  // integer → string
                Email: GetStringOrNull(json, "email"),
                EmailVerified: !string.IsNullOrEmpty(GetStringOrNull(json, "email")),  // GitHub email private olabilir
                DisplayName: GetStringOrNull(json, "name") ?? GetStringOrNull(json, "login")),

            _ => throw new NotSupportedException(provider)
        };
    }

    private static string? GetStringOrNull(JsonElement json, string property)
    {
        if (!json.TryGetProperty(property, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    private async Task<AuthResult> IssueTokensViaAuthServiceAsync(Customer customer, string? ip, string? userAgent)
    {
        // AuthService.IssueTokensAsync private — public bir helper gerek.
        // Geçici çözüm: customer'a "external_login" pseudo-password yoluyla token üret.
        // Production'da AuthService'e public bir IssueForExternalAsync ekle.
        customer.LastLoginUtc = DateTime.UtcNow;
        await _customerRepo.UpdateAsync(customer);

        return await _authService.IssueExternalTokensAsync(customer, ip, userAgent);
    }

    private static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private record StateEntry(string Provider, string ReturnUrl);
}
