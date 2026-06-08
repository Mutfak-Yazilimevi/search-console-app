using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Core.RequestScope;
using SearchConsoleApp.Services.Auth;
using SearchConsoleApp.Web.Controllers.Public.Auth;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;

namespace SearchConsoleApp.Web.Controllers.Web;

/// <summary>
/// Login halindeyken hesap işlemleri.
/// Route: /api/web/account/*
/// </summary>
public class AccountController : WebApiController
{
    private readonly IAuthService _authService;
    private readonly IRequestScope _scope;

    public AccountController(IAuthService authService, IRequestScope scope)
    {
        _authService = authService;
        _scope = scope;
    }

    /// <summary>Mevcut parolayla yeni parola belirle.</summary>
    [HttpPost("password/change")]
    [Audit("account.password_change")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        try
        {
            var success = await _authService.ChangePasswordAsync(
                customerId, req.CurrentPassword, req.NewPassword);
            if (!success)
                return Problem(statusCode: 401, title: "Mevcut parola hatalı.");
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex)
        {
            return Problem(statusCode: 400, title: ex.Message);
        }
    }
}
