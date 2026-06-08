using Serilog.Core;
using Serilog.Events;
using SearchConsoleApp.Core.RequestScope;

namespace SearchConsoleApp.Web.Framework.Logging;

/// <summary>
/// Her log event'ine audience, tenant_id, customer_id, correlation_id ekler.
///
/// Tek satırda log'a otomatik dahil olur:
///   "User logged in" → kendi alanları + Audience=Web, CustomerId=42, CorrelationId=abc
///
/// Structured logging tarafı (Elastic, Datadog, Seq) bu field'lara göre
/// filtreleyebilir: "audience:admin AND error" gibi sorgular.
/// </summary>
public class RequestScopeEnricher : ILogEventEnricher
{
    private readonly IServiceProvider _serviceProvider;

    public RequestScopeEnricher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // Scope DI scoped — service provider'dan o anki scope'u al
        // Background log (DI scope yok) için try/catch
        IRequestScope? scope;
        try
        {
            scope = _serviceProvider.GetService(typeof(IRequestScope)) as IRequestScope;
        }
        catch
        {
            return;
        }

        if (scope == null) return;

        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty("Audience", scope.Audience.ToString()));

        if (scope.TenantId.HasValue)
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("TenantId", scope.TenantId.Value));

        if (scope.CustomerId.HasValue)
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("CustomerId", scope.CustomerId.Value));

        if (!string.IsNullOrEmpty(scope.CorrelationId))
            logEvent.AddPropertyIfAbsent(
                propertyFactory.CreateProperty("CorrelationId", scope.CorrelationId));
    }
}
