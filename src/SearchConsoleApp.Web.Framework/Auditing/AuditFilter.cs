using Microsoft.AspNetCore.Mvc.Filters;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Services.Auditing;

namespace SearchConsoleApp.Web.Framework.Auditing;

/// <summary>
/// [Audit] attribute taşıyan action'ları yakalar ve AuditService'e log düşürür.
///
/// Outcome:
/// - Action exception fırlatırsa → failure (FailureReason: exception type)
/// - HTTP status 2xx → success
/// - HTTP status 4xx/5xx → failure (FailureReason: status code)
///
/// Bu filter global olarak kayıt edilir (Program.cs) — her controller'a manuel
/// attribute koymak gerek yok, [Audit] olan action'lar otomatik yakalanır.
/// </summary>
public class AuditFilter : IAsyncActionFilter, IScopedService
{
    private readonly IAuditService _auditService;

    public AuditFilter(IAuditService auditService) => _auditService = auditService;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Action'da [Audit] var mı?
        var auditAttr = context.ActionDescriptor.EndpointMetadata
            .OfType<AuditAttribute>()
            .FirstOrDefault();

        if (auditAttr == null)
        {
            await next();
            return;
        }

        ActionExecutedContext? executed = null;
        Exception? caught = null;

        try
        {
            executed = await next();
            caught = executed.Exception;
        }
        catch (Exception ex)
        {
            caught = ex;
            throw;
        }
        finally
        {
            await TryWriteAuditAsync(context, auditAttr, executed, caught);
        }
    }

    private async Task TryWriteAuditAsync(
        ActionExecutingContext context,
        AuditAttribute attr,
        ActionExecutedContext? executed,
        Exception? caught)
    {
        try
        {
            var (outcome, failureReason) = DetermineOutcome(context, executed, caught);

            // Target ID — route'tan çek (varsa)
            long? targetId = null;
            Guid? targetEntityId = null;
            if (!string.IsNullOrEmpty(attr.TargetIdRouteKey) &&
                context.RouteData.Values.TryGetValue(attr.TargetIdRouteKey, out var routeVal))
            {
                if (routeVal is string s)
                {
                    if (long.TryParse(s, out var lid)) targetId = lid;
                    else if (Guid.TryParse(s, out var gid)) targetEntityId = gid;
                }
            }

            await _auditService.LogAsync(new AuditEntry
            {
                Action = attr.Action,
                TargetType = attr.TargetType,
                TargetId = targetId,
                TargetEntityId = targetEntityId,
                Outcome = outcome,
                FailureReason = failureReason,
            });
        }
        catch
        {
            // Audit yazımı asla business action'ı bozmaz
        }
    }

    private static (string Outcome, string? FailureReason) DetermineOutcome(
        ActionExecutingContext context,
        ActionExecutedContext? executed,
        Exception? caught)
    {
        if (caught != null) return ("failure", caught.GetType().Name);

        var status = context.HttpContext.Response.StatusCode;
        if (status is >= 200 and < 300) return ("success", null);
        if (status >= 400) return ("failure", $"http_{status}");

        return ("success", null);
    }
}
