using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Core.RequestScope;
using SearchConsoleApp.Services.Privacy;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;

namespace SearchConsoleApp.Web.Controllers.Web;

public record DeleteAccountRequest(string Password, string Reason);

/// <summary>
/// GDPR — kullanıcının kendi verisi üzerinde haklar.
/// Route: /api/v1/web/account/privacy/*
/// (Base WebApiController "api/v1/web/[controller]" verir; "account/privacy"
/// alt yolu için route override edilir.)
/// </summary>
[Route("api/v{version:apiVersion}/web/account/privacy")]
public class PrivacyController : WebApiController
{
    private readonly IGdprService _gdpr;
    private readonly IRequestScope _scope;
    private readonly Services.Customers.ICustomerService _customers;
    private readonly Services.Security.IPasswordHasher _passwordHasher;

    public PrivacyController(
        IGdprService gdpr,
        IRequestScope scope,
        Services.Customers.ICustomerService customers,
        Services.Security.IPasswordHasher passwordHasher)
    {
        _gdpr = gdpr;
        _scope = scope;
        _customers = customers;
        _passwordHasher = passwordHasher;
    }

    /// <summary>Kendi verisini JSON olarak indir.</summary>
    [HttpGet("export")]
    [Audit("gdpr.export")]
    public async Task<IActionResult> Export()
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var json = await _gdpr.ExportCustomerDataAsync(customerId);
        return File(System.Text.Encoding.UTF8.GetBytes(json),
            "application/json",
            $"my-data-{DateTime.UtcNow:yyyy-MM-dd}.json");
    }

    /// <summary>"Hesabımı sil" — anonymize, soft-delete. Parola doğrulaması zorunlu.</summary>
    // NOT: [Audit] filtresi bilinçli olarak yok. Filtre action sonrası çalışıp
    // silinen kullanıcının PII'ını (email) yeni bir audit kaydına yazardı.
    // Bunun yerine GdprService anonymize tamamlandıktan SONRA "gdpr.anonymize"
    // kaydını manuel yazar (o noktada actor lookup'ı null email döner).
    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromBody] DeleteAccountRequest req)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var customer = await _customers.GetCustomerByIdAsync(customerId);
        if (customer == null || string.IsNullOrEmpty(customer.PasswordHash))
        {
            // OAuth-only kullanıcılar parola doğrulaması yapamaz —
            // burada ek bir akış gerek (örn. SMS / email confirmation linki)
            return Problem(statusCode: 400,
                title: "Bu hesapta parola yok. Lütfen önce parola belirleyin veya admin ile iletişime geçin.");
        }

        if (!_passwordHasher.Verify(req.Password, customer.PasswordHash))
            return Problem(statusCode: 401, title: "Parola hatalı.");

        await _gdpr.AnonymizeCustomerAsync(customerId, req.Reason ?? "user_self_delete");
        return Ok(new { ok = true });
    }
}
