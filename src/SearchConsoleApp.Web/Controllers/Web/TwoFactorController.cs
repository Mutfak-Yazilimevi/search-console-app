using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Core.RequestScope;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Services.Security;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;

namespace SearchConsoleApp.Web.Controllers.Web;

public record TwoFactorStartResponse(string Secret, string OtpAuthUri);

public record TwoFactorEnableRequest(string Secret, string Code);

public record TwoFactorEnableResponse(IReadOnlyList<string> RecoveryCodes);

public record TwoFactorDisableRequest(string Password);

public record TwoFactorStatusResponse(bool Enabled, int RemainingRecoveryCodes);

/// <summary>
/// Kullanıcının 2FA kurulumunu yönetir.
/// Route: /api/web/2fa/*
/// </summary>
public class TwoFactorController : WebApiController
{
    private readonly ITwoFactorService _twoFactor;
    private readonly ICustomerService _customers;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRequestScope _scope;

    public TwoFactorController(
        ITwoFactorService twoFactor,
        ICustomerService customers,
        IPasswordHasher passwordHasher,
        IRequestScope scope)
    {
        _twoFactor = twoFactor;
        _customers = customers;
        _passwordHasher = passwordHasher;
        _scope = scope;
    }

    /// <summary>2FA durumu.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var customer = await _customers.GetCustomerByIdAsync(customerId);
        if (customer == null) return Unauthorized();

        var remaining = string.IsNullOrEmpty(customer.RecoveryCodesHashes)
            ? 0
            : customer.RecoveryCodesHashes.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;

        return Ok(new TwoFactorStatusResponse(customer.TwoFactorEnabled, remaining));
    }

    /// <summary>Setup başlat: secret + QR URI döner. DB'ye yazılmaz.</summary>
    [HttpPost("setup")]
    [Audit("2fa.setup_start")]
    public async Task<IActionResult> StartSetup()
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var info = await _twoFactor.StartSetupAsync(customerId);
        return Ok(new TwoFactorStartResponse(info.Secret, info.OtpAuthUri));
    }

    /// <summary>İlk doğrulanan code 2FA'yı aktif eder. Recovery code'lar döner.</summary>
    [HttpPost("enable")]
    [Audit("2fa.enable")]
    public async Task<IActionResult> Enable([FromBody] TwoFactorEnableRequest req)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        try
        {
            var codes = await _twoFactor.EnableAsync(customerId, req.Secret, req.Code);
            return Ok(new TwoFactorEnableResponse(codes));
        }
        catch (UnauthorizedAccessException)
        {
            return Problem(statusCode: 400, title: "2FA kodu geçersiz.");
        }
    }

    /// <summary>2FA'yı kapat — parola doğrulaması gerekli.</summary>
    [HttpPost("disable")]
    [Audit("2fa.disable")]
    public async Task<IActionResult> Disable([FromBody] TwoFactorDisableRequest req)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var customer = await _customers.GetCustomerByIdAsync(customerId);
        if (customer == null || string.IsNullOrEmpty(customer.PasswordHash))
            return Unauthorized();

        if (!_passwordHasher.Verify(req.Password, customer.PasswordHash))
            return Problem(statusCode: 401, title: "Parola hatalı.");

        await _twoFactor.DisableAsync(customerId);
        return Ok(new { ok = true });
    }

    /// <summary>Recovery code'ları yeniden üret. Eski hepsi geçersizleşir.</summary>
    [HttpPost("recovery-codes/regenerate")]
    [Audit("2fa.recovery_regenerate")]
    public async Task<IActionResult> RegenerateRecoveryCodes()
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        try
        {
            var codes = await _twoFactor.RegenerateRecoveryCodesAsync(customerId);
            return Ok(new TwoFactorEnableResponse(codes));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: 400, title: ex.Message);
        }
    }
}
