using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Core.Auth;
using SearchConsoleApp.Services.Privacy;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;
using SearchConsoleApp.Web.Framework.Auth;

namespace SearchConsoleApp.Web.Controllers.Admin;

public record AdminDeleteCustomerRequest(string Reason);

/// <summary>
/// Admin: müşteri verisinin export ve anonymize'i.
/// Route: /api/v1/admin/privacy/*
/// Permission: customers.delete (GDPR action ağır yetki gerekir)
/// </summary>
[HasPermission(Permissions.CustomersDelete)]
public class PrivacyController : AdminApiController
{
    private readonly IGdprService _gdpr;
    public PrivacyController(IGdprService gdpr) => _gdpr = gdpr;

    /// <summary>Bir kullanıcının tüm verisini export et (örn. yasal talep).</summary>
    [HttpGet("customers/{customerId:long}/export")]
    [Audit("admin.gdpr.export")]
    public async Task<IActionResult> Export(long customerId)
    {
        try
        {
            var json = await _gdpr.ExportCustomerDataAsync(customerId);
            return File(System.Text.Encoding.UTF8.GetBytes(json),
                "application/json",
                $"customer-{customerId}-{DateTime.UtcNow:yyyy-MM-dd}.json");
        }
        catch (InvalidOperationException)
        {
            return NotFoundResult();
        }
    }

    /// <summary>Bir kullanıcının verisini anonymize et + soft-delete.</summary>
    [HttpPost("customers/{customerId:long}/anonymize")]
    [Audit("admin.gdpr.anonymize")]
    public async Task<IActionResult> Anonymize(long customerId, [FromBody] AdminDeleteCustomerRequest req)
    {
        try
        {
            await _gdpr.AnonymizeCustomerAsync(customerId, req.Reason ?? "admin_request");
            return Ok(new { ok = true });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: 400, title: ex.Message);
        }
    }
}
