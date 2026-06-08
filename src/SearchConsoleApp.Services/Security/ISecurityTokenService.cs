namespace SearchConsoleApp.Services.Security;

public interface ISecurityTokenService
{
    /// <summary>
    /// Yeni token üretir. Raw token client'a döner (email link'inde),
    /// DB'ye hash'i kaydedilir.
    ///
    /// Aynı customer + purpose için varolan aktif token'lar revoke edilir
    /// (tek seferde bir aktif token kuralı).
    /// </summary>
    Task<string> IssueAsync(long customerId, string purpose, TimeSpan ttl, string? ip = null);

    /// <summary>
    /// Token'ı consume eder: bulur, doğrular, UsedUtc set eder.
    /// Geçerliyse CustomerId döner, yoksa null.
    /// </summary>
    Task<long?> ConsumeAsync(string rawToken, string purpose);
}

public static class SecurityTokenPurposes
{
    public const string EmailVerification = "email_verification";
    public const string PasswordReset = "password_reset";
}
