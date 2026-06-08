using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SearchConsoleApp.Services.Auth;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;
using SearchConsoleApp.Web.Framework.RateLimiting;

namespace SearchConsoleApp.Web.Controllers.Public.Auth;

/// <summary>
/// Public auth endpoint'leri — anonim erişim.
/// Route: /api/public/auth/*
///
/// AuthPolicy uygulanır: IP+endpoint başına 10/dakika — brute force koruması.
/// </summary>
[EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
public class AuthController : PublicApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("login")]
    [Audit("auth.login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var (ip, ua) = GetClientInfo();
        try
        {
            var result = await _authService.LoginAsync(request.Email, request.Password, ip, ua);

            // 2FA gerekliyse: gerçek token verme, preAuthToken ile cevap ver
            if (result.RequiresTwoFactor)
            {
                return Ok(new TwoFactorRequiredResponse(
                    RequiresTwoFactor: true,
                    PreAuthToken: result.PreAuthToken!));
            }

            return Ok(ToTokens(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Problem(statusCode: 401, title: "Email veya şifre hatalı.");
        }
    }

    /// <summary>2FA login ikinci adımı — preAuth token + TOTP code.</summary>
    [HttpPost("login/2fa")]
    [Audit("auth.login_2fa")]
    public async Task<IActionResult> LoginTwoFactor([FromBody] TwoFactorLoginRequest request)
    {
        var (ip, ua) = GetClientInfo();
        try
        {
            var result = await _authService.LoginWithTwoFactorAsync(
                request.PreAuthToken, request.Code, request.UseRecoveryCode, ip, ua);
            return Ok(ToTokens(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(statusCode: 401, title: ex.Message);
        }
    }

    [HttpPost("register")]
    [Audit("auth.register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var (ip, ua) = GetClientInfo();
        try
        {
            var result = await _authService.RegisterAsync(
                request.Email, request.Password, request.FirstName, request.LastName, ip, ua);
            return Ok(ToTokens(result));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: 409, title: ex.Message);
        }
    }

    [HttpPost("refresh")]
    [Audit("auth.refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var (ip, ua) = GetClientInfo();
        try
        {
            var result = await _authService.RefreshAsync(request.RefreshToken, ip, ua);
            return Ok(ToTokens(result));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(statusCode: 401, title: ex.Message);
        }
    }

    [HttpPost("logout")]
    [Audit("auth.logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            await _authService.LogoutAsync(request.RefreshToken);

        return Ok(new { ok = true });
    }

    // === Email verification ===

    /// <summary>Verification linkini tekrar gönder (login halindeyken).</summary>
    [HttpPost("email/resend-verification")]
    [Audit("auth.email_verification_resend")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        var (ip, _) = GetClientInfo();

        // Email enumeration koruması: her durumda success dön
        // (gerçek lookup AuthService içinde, varsa gönderir)
        try
        {
            var customerId = await _authService.LookupCustomerIdByEmailAsync(request.Email);
            if (customerId.HasValue)
                await _authService.SendEmailVerificationAsync(customerId.Value, ip);
        }
        catch
        {
            // Sessiz
        }
        return Ok(new { ok = true });
    }

    /// <summary>Email verification token'ını kullan.</summary>
    [HttpPost("email/verify")]
    [Audit("auth.email_verify")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var success = await _authService.VerifyEmailAsync(request.Token);
        if (!success)
            return Problem(statusCode: 400, title: "Token geçersiz veya süresi dolmuş.");
        return Ok(new { ok = true });
    }

    // === Password reset ===

    /// <summary>"Şifremi unuttum" — reset linkini email'e gönderir.</summary>
    [HttpPost("password/forgot")]
    [Audit("auth.password_forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var (ip, _) = GetClientInfo();
        // Kullanıcı enumeration koruması: her durumda success
        await _authService.RequestPasswordResetAsync(request.Email, ip);
        return Ok(new { ok = true });
    }

    /// <summary>Reset token ile yeni şifre belirle.</summary>
    [HttpPost("password/reset")]
    [Audit("auth.password_reset")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            var success = await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
            if (!success)
                return Problem(statusCode: 400, title: "Token geçersiz veya süresi dolmuş.");
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: 400, title: ex.Message);
        }
    }

    // === Helpers ===

    private AuthTokens ToTokens(AuthResult r) => new(
        r.AccessToken!,
        r.AccessTokenExpiresAt!.Value,
        r.RefreshToken!,
        r.RefreshTokenExpiresAt!.Value,
        new UserInfo(
            r.Customer!.EntityId,
            r.Customer.Email,
            r.Customer.FirstName,
            r.Customer.LastName,
            r.Customer.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToList()
        ));

    private (string? Ip, string? UserAgent) GetClientInfo()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = Request.Headers.UserAgent.ToString();
        return (ip, string.IsNullOrWhiteSpace(ua) ? null : ua);
    }
}
