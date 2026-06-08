using Asp.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace SearchConsoleApp.Web.Framework.Versioning;

public static class VersioningSetup
{
    /// <summary>
    /// API versioning + ApiExplorer (Swagger version-aware).
    ///
    /// Default version: 1.0
    /// Version reader: URL segment ("v1")
    /// Reporting: response header'da 'api-supported-versions' ile bilgi döner
    /// </summary>
    public static IServiceCollection AddSearchConsoleAppVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(o =>
            {
                o.DefaultApiVersion = new ApiVersion(1, 0);
                o.ReportApiVersions = true;
                o.AssumeDefaultVersionWhenUnspecified = false;  // explicit version zorunlu
                o.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(o =>
            {
                o.GroupNameFormat = "'v'VVV";   // v1, v1.1, v2 vb.
                o.SubstituteApiVersionInUrl = true;
            });

        return services;
    }
}
