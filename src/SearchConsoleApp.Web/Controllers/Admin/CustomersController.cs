using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Web.Framework.Api;

namespace SearchConsoleApp.Web.Controllers.Admin;

/// <summary>
/// ÖRNEK Admin API controller — sadece admin rolü.
/// Route: /api/admin/customers
/// Swagger doc: "admin"
/// JWT zorunlu (Authorize policy: Admin).
/// </summary>
public class CustomersController : AdminApiController
{
    private readonly ICustomerService _customerService;
    public CustomersController(ICustomerService customerService) => _customerService = customerService;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool onlyActive = true)
    {
        var items = await _customerService.GetAllCustomersAsync(onlyActive);
        return Ok(items.Select(c => new
        {
            c.EntityId,
            c.Email,
            c.FirstName,
            c.LastName,
            c.Active,
            c.CreatedOnUtc
        }));
    }

    [HttpGet("{entityId:guid}")]
    public async Task<IActionResult> Get(Guid entityId)
    {
        var customer = await _customerService.GetCustomerByEntityIdAsync(entityId);
        if (customer == null) return NotFoundResult();
        return Ok(customer);
    }

    [HttpDelete("{entityId:guid}")]
    public async Task<IActionResult> Delete(Guid entityId)
    {
        var customer = await _customerService.GetCustomerByEntityIdAsync(entityId);
        if (customer == null) return NotFoundResult();
        await _customerService.DeleteCustomerAsync(customer);  // soft delete
        return NoContent();
    }
}
