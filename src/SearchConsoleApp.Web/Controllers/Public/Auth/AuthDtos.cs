using System.ComponentModel.DataAnnotations;

namespace SearchConsoleApp.Web.Controllers.Public.Auth;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password);

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    string? FirstName,
    string? LastName);

public record RefreshRequest([Required] string RefreshToken);

public record LogoutRequest(string? RefreshToken);

public record TwoFactorLoginRequest(
    [Required] string PreAuthToken,
    [Required] string Code,
    bool UseRecoveryCode = false);

public record TwoFactorRequiredResponse(
    bool RequiresTwoFactor,
    string PreAuthToken);

public record ResendVerificationRequest([Required, EmailAddress] string Email);

public record VerifyEmailRequest([Required] string Token);

public record ForgotPasswordRequest([Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

public record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserInfo User);

public record UserInfo(
    Guid EntityId,
    string Email,
    string? FirstName,
    string? LastName,
    IReadOnlyList<string> Roles);
