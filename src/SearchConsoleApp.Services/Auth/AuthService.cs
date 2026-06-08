using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Auth;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Email;
using SearchConsoleApp.Services.Identity;
using SearchConsoleApp.Services.Localization;
using SearchConsoleApp.Services.Security;

namespace SearchConsoleApp.Services.Auth;

/// <summary>
/// Auth iş mantığı (Device + Session entegrasyonlu):
/// - Register/Login: yeni Device (varsa mevcut) + DeviceSession + RefreshToken
/// - Refresh: rotation → eski Session revoke, yeni Session başlat
/// - Logout: Session revoke + RefreshToken revoke
/// - RevokeAll: tüm Session'lar + RefreshToken'lar revoke (panic logout)
///
/// IRequestScope'tan IP/Audience okunur. Device fingerprint header'dan
/// (X-Device-Fingerprint, X-Platform) gelir veya UA'dan üretilir.
/// </summary>
public partial class AuthService : IAuthService, IScopedService
{
    private readonly IRepository<Customer> _customerRepository;
    private readonly IRepository<RefreshToken> _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtIssuer _jwtIssuer;
    private readonly IDeviceService _deviceService;
    private readonly ISessionService _sessionService;
    private readonly ITwoFactorService _twoFactor;
    private readonly IPreAuthTokenStore _preAuthStore;
    private readonly ISecurityTokenService _securityTokens;
    private readonly IEmailSender _emailSender;
    private readonly ILocalizationService _localizer;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _config;
    private readonly int _refreshTokenDays;

    public AuthService(
        IRepository<Customer> customerRepository,
        IRepository<RefreshToken> refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtIssuer jwtIssuer,
        IDeviceService deviceService,
        ISessionService sessionService,
        ITwoFactorService twoFactor,
        IPreAuthTokenStore preAuthStore,
        ISecurityTokenService securityTokens,
        IEmailSender emailSender,
        ILocalizationService localizer,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration config)
    {
        _customerRepository = customerRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtIssuer = jwtIssuer;
        _deviceService = deviceService;
        _sessionService = sessionService;
        _twoFactor = twoFactor;
        _preAuthStore = preAuthStore;
        _securityTokens = securityTokens;
        _emailSender = emailSender;
        _localizer = localizer;
        _httpContextAccessor = httpContextAccessor;
        _config = config;
        _refreshTokenDays = int.Parse(config["Jwt:RefreshTokenDays"] ?? "30");
    }

    public virtual async Task<AuthResult> RegisterAsync(
        string email, string password, string? firstName, string? lastName,
        string? ip, string? userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var existing = await _customerRepository.Table
            .FirstOrDefaultAsync(c => c.Email == normalizedEmail);
        if (existing != null)
            throw new InvalidOperationException(_localizer.Get("auth.duplicate_email"));

        var customer = new Customer
        {
            Email = normalizedEmail,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = _passwordHasher.Hash(password),
            Active = true,
            EmailConfirmed = false,
            Roles = "user",
            CreatedOnUtc = DateTime.UtcNow,
        };
        await _customerRepository.InsertAsync(customer);

        // Email verification linkini arka planda gönder — register'ı bekletme
        // Best-effort: email başarısızsa register yine de başarılı sayılır.
        try
        {
            await SendEmailVerificationAsync(customer.Id, ip);
        }
        catch
        {
            // Sessiz: kullanıcı sonradan "tekrar gönder" diyebilir
        }

        return await IssueTokensAsync(customer, ip, userAgent, Audience.Web);
    }

    public virtual async Task<AuthResult> LoginAsync(string email, string password, string? ip, string? userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        var customer = await _customerRepository.Table
            .FirstOrDefaultAsync(c => c.Email == normalizedEmail);

        // PasswordHash null olabilir (OAuth-only kullanıcı) — bu durumda parola
        // login yolu kapalı, kullanıcı OAuth provider'ı kullanmalı.
        if (customer == null || string.IsNullOrEmpty(customer.PasswordHash))
            throw new UnauthorizedAccessException(_localizer.Get("auth.invalid_credentials"));

        if (!customer.Active)
            throw new UnauthorizedAccessException(_localizer.Get("auth.account_disabled"));

        if (!_passwordHasher.Verify(password, customer.PasswordHash))
            throw new UnauthorizedAccessException(_localizer.Get("auth.invalid_credentials"));

        // 2FA aktif mi? Aktifse Device.Trusted bypass kontrolü
        if (customer.TwoFactorEnabled)
        {
            var device = await GetOrCreateDeviceAsync(customer.Id, userAgent);
            if (!device.Trusted)
            {
                // 2FA gerekli — PreAuth token üret, gerçek token VERME
                var preAuthToken = await _preAuthStore.CreateAsync(customer.Id, TimeSpan.FromMinutes(5));
                return new AuthResult(
                    Customer: null, AccessToken: null, AccessTokenExpiresAt: null,
                    RefreshToken: null, RefreshTokenExpiresAt: null,
                    RequiresTwoFactor: true, PreAuthToken: preAuthToken);
            }
            // Trusted device: 2FA atlanır, normal login akışı
        }

        customer.LastLoginUtc = DateTime.UtcNow;
        await _customerRepository.UpdateAsync(customer);

        var audience = customer.Roles.Contains("admin", StringComparison.OrdinalIgnoreCase)
            ? Audience.Admin : Audience.Web;
        return await IssueTokensAsync(customer, ip, userAgent, audience);
    }

    public virtual async Task<AuthResult> LoginWithTwoFactorAsync(
        string preAuthToken, string code, bool useRecoveryCode, string? ip, string? userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preAuthToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var customerId = await _preAuthStore.ConsumeAsync(preAuthToken)
            ?? throw new UnauthorizedAccessException(_localizer.Get("auth.preauth_invalid"));

        var customer = await _customerRepository.GetByIdAsync(customerId)
            ?? throw new UnauthorizedAccessException(_localizer.Get("auth.account_disabled"));

        var verified = useRecoveryCode
            ? await _twoFactor.VerifyRecoveryCodeAsync(customerId, code)
            : await _twoFactor.VerifyAsync(customerId, code);

        if (!verified)
            throw new UnauthorizedAccessException(_localizer.Get("auth.2fa_invalid"));

        customer.LastLoginUtc = DateTime.UtcNow;
        await _customerRepository.UpdateAsync(customer);

        var audience = customer.Roles.Contains("admin", StringComparison.OrdinalIgnoreCase)
            ? Audience.Admin : Audience.Web;
        return await IssueTokensAsync(customer, ip, userAgent, audience);
    }

    public virtual async Task<AuthResult> RefreshAsync(string refreshToken, string? ip, string? userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        var hash = HashToken(refreshToken);
        var stored = await _refreshTokenRepository.Table
            .FirstOrDefaultAsync(t => t.TokenHash == hash);

        if (stored == null || !stored.IsActive)
            throw new UnauthorizedAccessException(_localizer.Get("auth.token_invalid"));

        var customer = await _customerRepository.GetByIdAsync(stored.CustomerId);
        if (customer == null || !customer.Active)
            throw new UnauthorizedAccessException(_localizer.Get("auth.account_disabled"));

        // Rotation: eski refresh ve session'ı revoke et
        stored.RevokedOnUtc = DateTime.UtcNow;

        var (newRefresh, newRefreshHash, newRefreshExpires) = GenerateRefreshToken();
        stored.ReplacedByTokenHash = newRefreshHash;
        await _refreshTokenRepository.UpdateAsync(stored);

        // Eski session'ı rotation reason ile kapat
        await _sessionService.RevokeByRefreshTokenAsync(hash, "rotation");

        // Yeni RefreshToken kaydı
        var entity = new RefreshToken
        {
            CustomerId = customer.Id,
            TokenHash = newRefreshHash,
            CreatedOnUtc = DateTime.UtcNow,
            ExpiresOnUtc = newRefreshExpires,
            CreatedByIp = ip,
            UserAgent = userAgent,
        };
        await _refreshTokenRepository.InsertAsync(entity);

        // Yeni session — aynı device ile devam
        var audience = customer.Roles.Contains("admin", StringComparison.OrdinalIgnoreCase)
            ? Audience.Admin : Audience.Web;
        var device = await GetOrCreateDeviceAsync(customer.Id, userAgent);
        var newSession = await _sessionService.StartAsync(customer.Id, device.Id, audience, newRefreshHash, ip, userAgent);

        var (accessToken, accessExpires) = _jwtIssuer.IssueAccessToken(customer, newSession.Id);
        return new AuthResult(customer, accessToken, accessExpires, newRefresh, newRefreshExpires);
    }

    public virtual async Task LogoutAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        var hash = HashToken(refreshToken);
        var stored = await _refreshTokenRepository.Table
            .FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored == null) return;

        // Hem RefreshToken hem Session revoke
        if (stored.RevokedOnUtc == null)
        {
            stored.RevokedOnUtc = DateTime.UtcNow;
            await _refreshTokenRepository.UpdateAsync(stored);
        }
        await _sessionService.RevokeByRefreshTokenAsync(hash, "user");
    }

    public virtual async Task RevokeAllAsync(long customerId)
    {
        // Tüm RefreshToken'ları revoke et
        var tokens = await _refreshTokenRepository.Table
            .Where(t => t.CustomerId == customerId && t.RevokedOnUtc == null)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var t in tokens)
        {
            t.RevokedOnUtc = now;
            await _refreshTokenRepository.UpdateAsync(t, publishEvent: false);
        }

        // Tüm session'ları revoke et
        await _sessionService.RevokeAllExceptAsync(customerId, exceptSessionId: 0, reason: "security");
    }

    // === Email verification ===

    public virtual async Task SendEmailVerificationAsync(long customerId, string? ip = null)
    {
        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer == null) return;
        if (customer.EmailConfirmed) return;  // Zaten doğrulanmış, gönderme

        var rawToken = await _securityTokens.IssueAsync(
            customerId, SecurityTokenPurposes.EmailVerification,
            TimeSpan.FromHours(24), ip);

        var appUrl = _config["App:PublicUrl"]?.TrimEnd('/')
            ?? "http://localhost:4200";
        var verifyUrl = $"{appUrl}/verify-email?token={Uri.EscapeDataString(rawToken)}";
        var appName = _config["App:Name"] ?? "SearchConsoleApp";

        var message = EmailTemplates.EmailVerification(customer.Email, verifyUrl, appName);

        // Fail-tolerant: email gönderimi başarısızsa user-facing operasyonu bozma
        try
        {
            await _emailSender.SendAsync(message);
        }
        catch
        {
            // Gerçek production'da: queue'ya at, retry yap. Şimdilik sessiz catch.
            // Audit zaten "auth.email_verification_sent" failure ile log'lar.
            throw;
        }
    }

    public virtual async Task<bool> VerifyEmailAsync(string token)
    {
        var customerId = await _securityTokens.ConsumeAsync(token, SecurityTokenPurposes.EmailVerification);
        if (customerId == null) return false;

        var customer = await _customerRepository.GetByIdAsync(customerId.Value);
        if (customer == null) return false;

        customer.EmailConfirmed = true;
        await _customerRepository.UpdateAsync(customer);
        return true;
    }

    // === Password reset ===

    public virtual async Task RequestPasswordResetAsync(string email, string? ip = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        // KULLANICI ENUMERATİON KORUMASI:
        // Email var/yok bilgisini sızdırma — her durumda success dön.
        // Email gerçekten varsa mail gönder, yoksa sessizce no-op.
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await _customerRepository.Table
            .FirstOrDefaultAsync(c => c.Email == normalizedEmail);

        if (customer == null || !customer.Active) return;  // sessiz

        var rawToken = await _securityTokens.IssueAsync(
            customer.Id, SecurityTokenPurposes.PasswordReset,
            TimeSpan.FromHours(1), ip);

        var appUrl = _config["App:PublicUrl"]?.TrimEnd('/')
            ?? "http://localhost:4200";
        var resetUrl = $"{appUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        var appName = _config["App:Name"] ?? "SearchConsoleApp";

        var message = EmailTemplates.PasswordReset(customer.Email, resetUrl, appName);

        try
        {
            await _emailSender.SendAsync(message);
        }
        catch
        {
            // Sessiz: email başarısızsa kullanıcıya "gönderdik" demeye devam et.
            // Audit log'da failure görünür.
        }
    }

    public virtual async Task<bool> ResetPasswordAsync(string token, string newPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);
        if (newPassword.Length < 8)
            throw new ArgumentException(_localizer.Get("auth.password_min_length"), nameof(newPassword));

        var customerId = await _securityTokens.ConsumeAsync(token, SecurityTokenPurposes.PasswordReset);
        if (customerId == null) return false;

        var customer = await _customerRepository.GetByIdAsync(customerId.Value);
        if (customer == null) return false;

        customer.PasswordHash = _passwordHasher.Hash(newPassword);
        await _customerRepository.UpdateAsync(customer);

        // GÜVENLİK: şifre değişiminde TÜM aktif session'ları revoke et
        // (saldırgan zaten içerideyse atılsın)
        await RevokeAllAsync(customer.Id);

        return true;
    }

    public virtual async Task<bool> ChangePasswordAsync(long customerId, string currentPassword, string newPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);
        if (newPassword.Length < 8)
            throw new ArgumentException(_localizer.Get("auth.password_min_length"), nameof(newPassword));

        var customer = await _customerRepository.GetByIdAsync(customerId);
        if (customer == null || string.IsNullOrEmpty(customer.PasswordHash)) return false;

        if (!_passwordHasher.Verify(currentPassword, customer.PasswordHash))
            return false;

        customer.PasswordHash = _passwordHasher.Hash(newPassword);
        await _customerRepository.UpdateAsync(customer);

        // Şifre değiştiyse diğer cihazlardaki oturumları kapat (mevcut hariç)
        // Mevcut session bilgisi RequestScope'tan gelirse onu hariç tut
        // (caller controller'da current session'ı pas edebilir)

        return true;
    }

    public virtual async Task<long?> LookupCustomerIdByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalized = email.Trim().ToLowerInvariant();
        var id = await _customerRepository.Table
            .Where(c => c.Email == normalized)
            .Select(c => (long?)c.Id)
            .FirstOrDefaultAsync();
        return id;
    }

    public virtual Task<AuthResult> IssueExternalTokensAsync(Customer customer, string? ip, string? userAgent)
    {
        // OAuth/SSO sonrası: 2FA atlanır (provider zaten doğruladı).
        // Audience: customer.Roles'a göre admin veya web.
        var audience = customer.Roles.Contains("admin", StringComparison.OrdinalIgnoreCase)
            ? Audience.Admin : Audience.Web;

        return IssueTokensAsync(customer, ip, userAgent, audience);
    }

    // === Internals ===

    private async Task<AuthResult> IssueTokensAsync(Customer customer, string? ip, string? userAgent, Audience audience)
    {
        var (refresh, refreshHash, refreshExpires) = GenerateRefreshToken();

        await _refreshTokenRepository.InsertAsync(new RefreshToken
        {
            CustomerId = customer.Id,
            TokenHash = refreshHash,
            CreatedOnUtc = DateTime.UtcNow,
            ExpiresOnUtc = refreshExpires,
            CreatedByIp = ip,
            UserAgent = userAgent,
        });

        // Device + Session — JWT üretmeden ÖNCE oluştur, session.Id'yi token'a koyacağız
        var device = await GetOrCreateDeviceAsync(customer.Id, userAgent);
        var session = await _sessionService.StartAsync(customer.Id, device.Id, audience, refreshHash, ip, userAgent);

        // Access token session.Id ile birlikte üretilir
        var (accessToken, accessExpires) = _jwtIssuer.IssueAccessToken(customer, session.Id);

        return new AuthResult(customer, accessToken, accessExpires, refresh, refreshExpires);
    }

    private async Task<Core.Domain.Identity.Device> GetOrCreateDeviceAsync(long customerId, string? userAgent)
    {
        var http = _httpContextAccessor.HttpContext;
        var input = new FingerprintInput(
            UserAgent: userAgent ?? http?.Request.Headers.UserAgent.ToString(),
            AcceptLanguage: http?.Request.Headers.AcceptLanguage.ToString(),
            Platform: http?.Request.Headers["X-Platform"].ToString(),
            ClientHint: http?.Request.Headers["X-Device-Fingerprint"].ToString());

        var deviceType = DetermineDeviceType(input);
        return await _deviceService.GetOrCreateAsync(customerId, input, deviceType);
    }

    private static string DetermineDeviceType(FingerprintInput input)
    {
        var ua = (input.UserAgent ?? "").ToLowerInvariant();
        var platform = (input.Platform ?? "").ToLowerInvariant();

        if (platform == "ios" || ua.Contains("iphone") || ua.Contains("ipad")) return "mobile-ios";
        if (platform == "android" || ua.Contains("android")) return "mobile-android";
        if (ua.Contains("mobile")) return "mobile-other";
        return "web";
    }

    private (string Token, string Hash, DateTime ExpiresAt) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var hash = HashToken(token);
        var expires = DateTime.UtcNow.AddDays(_refreshTokenDays);
        return (token, hash, expires);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
