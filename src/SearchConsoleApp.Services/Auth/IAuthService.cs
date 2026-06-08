using SearchConsoleApp.Core.Domain.Customers;

namespace SearchConsoleApp.Services.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string? firstName, string? lastName, string? ip, string? userAgent);

    /// <summary>
    /// Login. 2FA gerekliyse `AuthResult.RequiresTwoFactor=true` + PreAuth token döner;
    /// gerçek access/refresh token VERİLMEZ. Frontend code istemek için ekrana geçer,
    /// `LoginWithTwoFactorAsync` ile devam eder.
    /// </summary>
    Task<AuthResult> LoginAsync(string email, string password, string? ip, string? userAgent);

    /// <summary>
    /// 2FA gerektiren login'in ikinci adımı. PreAuth token + code verilir.
    /// </summary>
    Task<AuthResult> LoginWithTwoFactorAsync(string preAuthToken, string code, bool useRecoveryCode,
                                              string? ip, string? userAgent);

    Task<AuthResult> RefreshAsync(string refreshToken, string? ip, string? userAgent);
    Task LogoutAsync(string refreshToken);
    Task RevokeAllAsync(long customerId);

    // === Email verification ===

    /// <summary>Email doğrulama linki gönder (yeni token üretip emaile mailler).</summary>
    Task SendEmailVerificationAsync(long customerId, string? ip = null);

    /// <summary>Token ile email'i doğrula. EmailConfirmed=true set eder.</summary>
    Task<bool> VerifyEmailAsync(string token);

    // === Password reset ===

    /// <summary>
    /// Şifre sıfırlama linki gönder. Email yoksa SESSİZ no-op
    /// (kullanıcı enumeration koruması) — caller her zaman success döner.
    /// </summary>
    Task RequestPasswordResetAsync(string email, string? ip = null);

    /// <summary>Token ile yeni şifre belirle.</summary>
    Task<bool> ResetPasswordAsync(string token, string newPassword);

    /// <summary>Login halindeyken şifre değiştir (eski parola doğrulamasıyla).</summary>
    Task<bool> ChangePasswordAsync(long customerId, string currentPassword, string newPassword);

    /// <summary>
    /// Helper: email'den customer ID'yi çek. Public endpoint'lerde controller
    /// enumeration koruması yaparken kullanır (yoksa null döner, akış değişmez).
    /// </summary>
    Task<long?> LookupCustomerIdByEmailAsync(string email);

    /// <summary>
    /// OAuth/SSO sonrası token üret. ExternalAuthService bunu çağırır.
    /// Parola olmadan (PasswordHash null olabilir).
    /// </summary>
    Task<AuthResult> IssueExternalTokensAsync(Customer customer, string? ip, string? userAgent);
}

public record AuthResult(
    Customer? Customer,
    string? AccessToken,
    DateTime? AccessTokenExpiresAt,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt,
    bool RequiresTwoFactor = false,
    string? PreAuthToken = null);
