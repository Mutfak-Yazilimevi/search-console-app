using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace SearchConsoleApp.Web.Framework.Logging;

public static class SerilogSetup
{
    /// <summary>
    /// Serilog kurulumu — JSON output (production'da Elastic/Datadog'a stream'lenir),
    /// RequestScopeEnricher ile audience/tenant otomatik eklenir.
    ///
    /// Console + file sink, prod'da seq/elastic ek edilir.
    /// </summary>
    public static WebApplicationBuilder UseSearchConsoleAppSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, config) =>
        {
            config
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.With(new RequestScopeEnricher(services))
                .WriteTo.Console(formatter: new Serilog.Formatting.Compact.RenderedCompactJsonFormatter());
        });

        return builder;
    }
}
