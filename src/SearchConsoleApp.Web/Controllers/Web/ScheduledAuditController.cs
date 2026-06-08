using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Services.Audit;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Web.Framework.Api;

namespace SearchConsoleApp.Web.Controllers.Web;

public record CreateScheduledAuditApiRequest(
    string Url,
    string? Label,
    string? SearchConsolePropertyUrl,
    string? MigrationSourceUrl,
    string? Ga4PropertyId,
    string? WebhookUrl,
    bool NotifyOnComplete = true,
    bool NotifyOnCriticalOnly = false,
    int IntervalDays = 7);

public record UpdateScheduledAuditApiRequest(
    string? Label,
    string? SearchConsolePropertyUrl,
    string? MigrationSourceUrl,
    string? Ga4PropertyId,
    string? WebhookUrl,
    bool? NotifyOnComplete,
    bool? NotifyOnCriticalOnly,
    int? IntervalDays,
    bool? IsEnabled);

[Route("api/v{version:apiVersion}/web/audit/schedules")]
public class ScheduledAuditController : WebApiController
{
    private readonly IScheduledAuditService _scheduleService;
    private readonly ICustomerService _customerService;

    public ScheduledAuditController(
        IScheduledAuditService scheduleService,
        ICustomerService customerService)
    {
        _scheduleService = scheduleService;
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        var items = await _scheduleService.ListAsync(customer.Id, cancellationToken);
        return Ok(items.Select(MapSchedule));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateScheduledAuditApiRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Url))
            return ValidationProblem(new Dictionary<string, string[]> { ["url"] = ["URL is required."] });

        try
        {
            var schedule = await _scheduleService.CreateAsync(customer.Id, new CreateScheduledAuditRequest(
                request.Url,
                request.Label,
                request.SearchConsolePropertyUrl,
                request.MigrationSourceUrl,
                request.Ga4PropertyId,
                request.WebhookUrl,
                request.NotifyOnComplete,
                request.NotifyOnCriticalOnly,
                request.IntervalDays), cancellationToken);

            return Ok(MapSchedule(schedule));
        }
        catch (ArgumentException ex)
        {
            return ValidationProblem(new Dictionary<string, string[]> { ["url"] = [ex.Message] });
        }
    }

    [HttpPatch("{entityId:guid}")]
    public async Task<IActionResult> Update(
        Guid entityId,
        [FromBody] UpdateScheduledAuditApiRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        var schedule = await _scheduleService.UpdateAsync(entityId, customer.Id, new UpdateScheduledAuditRequest(
            request.Label,
            request.SearchConsolePropertyUrl,
            request.MigrationSourceUrl,
            request.Ga4PropertyId,
            request.WebhookUrl,
            request.NotifyOnComplete,
            request.NotifyOnCriticalOnly,
            request.IntervalDays,
            request.IsEnabled), cancellationToken);

        if (schedule == null) return NotFound();
        return Ok(MapSchedule(schedule));
    }

    [HttpDelete("{entityId:guid}")]
    public async Task<IActionResult> Delete(Guid entityId, CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        var deleted = await _scheduleService.DeleteAsync(entityId, customer.Id, cancellationToken);
        if (!deleted) return NotFound();
        return Ok(new { ok = true });
    }

    private async Task<Core.Domain.Customers.Customer?> GetCurrentCustomerAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var entityId)) return null;
        return await _customerService.GetCustomerByEntityIdAsync(entityId);
    }

    private static object MapSchedule(Core.Domain.Audit.ScheduledAudit s) => new
    {
        s.EntityId,
        s.Label,
        s.Url,
        s.SearchConsolePropertyUrl,
        s.MigrationSourceUrl,
        s.Ga4PropertyId,
        s.WebhookUrl,
        s.NotifyOnComplete,
        s.NotifyOnCriticalOnly,
        s.IntervalDays,
        s.NextRunUtc,
        s.IsEnabled,
        s.CreatedAtUtc,
        s.UpdatedAtUtc,
    };
}

[Route("api/v{version:apiVersion}/web/audit/dashboard")]
public class AuditDashboardController : WebApiController
{
    private readonly IScheduledAuditService _scheduleService;
    private readonly ICustomerService _customerService;

    public AuditDashboardController(
        IScheduledAuditService scheduleService,
        ICustomerService customerService)
    {
        _scheduleService = scheduleService;
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        var rows = await _scheduleService.GetDashboardAsync(customer.Id, cancellationToken);
        return Ok(rows);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? url,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return Unauthorized();

        var rows = await _scheduleService.GetHistoryAsync(customer.Id, url, limit, cancellationToken);
        return Ok(rows);
    }

    private async Task<Core.Domain.Customers.Customer?> GetCurrentCustomerAsync()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var entityId)) return null;
        return await _customerService.GetCustomerByEntityIdAsync(entityId);
    }
}
