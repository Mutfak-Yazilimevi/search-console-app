using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SearchConsoleApp.Services.Audit;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.RateLimiting;

namespace SearchConsoleApp.Web.Controllers.Web;

public record StartConnectedAuditRequest(string Url, string? SearchConsolePropertyUrl);

[Route("api/v{version:apiVersion}/web/audit")]
public class ConnectedAuditController : WebApiController
{
    private readonly IAuditService _auditService;
    private readonly ICustomerService _customerService;

    public ConnectedAuditController(IAuditService auditService, ICustomerService customerService)
    {
        _auditService = auditService;
        _customerService = customerService;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitingSetup.AuditPolicy)]
    public async Task<IActionResult> Start([FromBody] StartConnectedAuditRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
            return ValidationProblem(new Dictionary<string, string[]> { ["url"] = ["URL is required."] });

        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        try
        {
            var run = await _auditService.StartConnectedAuditAsync(
                request.Url, customer.Id, request.SearchConsolePropertyUrl, cancellationToken);
            return Ok(MapRun(run));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(new Dictionary<string, string[]> { ["url"] = [ex.Message] });
        }
        catch (AuditQuotaException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = ex.Message });
        }
    }

    private async Task<Core.Domain.Customers.Customer?> GetCurrentCustomerAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var entityId)) return null;
        return await _customerService.GetCustomerByEntityIdAsync(entityId);
    }

    private static object MapRun(Core.Domain.Audit.AuditRun run) => new
    {
        run.EntityId,
        run.InputUrl,
        run.NormalizedUrl,
        Status = run.Status.ToString(),
        Mode = run.Mode.ToString(),
        run.CreatedAt,
        run.StartedAt,
        run.CompletedAt,
        run.PagesCrawled,
        run.IssuesFound,
        run.CriticalCount,
        run.WarningCount,
        run.InfoCount,
        run.Score,
        run.ErrorMessage,
        run.SearchConsolePropertyUrl,
    };
}
