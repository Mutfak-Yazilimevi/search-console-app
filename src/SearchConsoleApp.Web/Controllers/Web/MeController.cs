using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Web.Framework.Api;

namespace SearchConsoleApp.Web.Controllers.Web;

/// <summary>
/// ÖRNEK Web API controller — giriş yapmış kullanıcı.
/// Route: /api/web/me
/// Swagger doc: "web"
/// JWT zorunlu (Authorize policy: WebUser).
/// </summary>
public class MeController : WebApiController
{
    private readonly ICustomerService _customerService;
    public MeController(ICustomerService customerService) => _customerService = customerService;

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        // JWT'den EntityId (sub) claim'i ile lookup
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var entityId))
            return Unauthorized();

        var customer = await _customerService.GetCustomerByEntityIdAsync(entityId);
        if (customer == null) return NotFoundResult("Customer not found");

        return Ok(new
        {
            customer.EntityId,
            customer.Email,
            customer.FirstName,
            customer.LastName
        });
    }
}
