using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Services.Audit;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Web.Framework.Api;

namespace SearchConsoleApp.Web.Controllers.Web;

public record CreateKeywordWatchRequest(string SiteUrl, string Keyword);

[Route("api/v{version:apiVersion}/web/audit/keyword-watches")]
public class KeywordWatchController : WebApiController
{
    private readonly ISiteKeywordWatchService _watchService;
    private readonly ICustomerService _customerService;

    public KeywordWatchController(
        ISiteKeywordWatchService watchService,
        ICustomerService customerService)
    {
        _watchService = watchService;
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? siteUrl, CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        var items = await _watchService.ListAsync(customer.Id, siteUrl, cancellationToken);
        return Ok(items.Select(w => new
        {
            w.EntityId,
            w.SiteHost,
            w.Keyword,
            w.IsEnabled,
            w.CreatedAtUtc,
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateKeywordWatchRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.SiteUrl) || string.IsNullOrWhiteSpace(request.Keyword))
            return ValidationProblem(new Dictionary<string, string[]>
            {
                ["siteUrl"] = ["Site URL is required."],
                ["keyword"] = ["Keyword is required."],
            });

        try
        {
            var watch = await _watchService.CreateAsync(
                customer.Id, request.SiteUrl, request.Keyword, cancellationToken);
            return Ok(new { watch.EntityId, watch.SiteHost, watch.Keyword, watch.CreatedAtUtc });
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(new Dictionary<string, string[]> { ["keyword"] = [ex.Message] });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { title = ex.Message });
        }
    }

    [HttpDelete("{entityId:guid}")]
    public async Task<IActionResult> Delete(Guid entityId, CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        var deleted = await _watchService.DeleteAsync(entityId, customer.Id, cancellationToken);
        if (!deleted) return NotFound();
        return Ok(new { ok = true });
    }

    private async Task<Core.Domain.Customers.Customer?> GetCurrentCustomerAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var entityId)) return null;
        return await _customerService.GetCustomerByEntityIdAsync(entityId);
    }
}
