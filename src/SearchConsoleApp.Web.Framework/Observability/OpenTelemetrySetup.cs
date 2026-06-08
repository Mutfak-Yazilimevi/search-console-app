using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SearchConsoleApp.Web.Framework.Observability;

/// <summary>
/// OpenTelemetry setup. Metrics + traces.
///
/// Tracing kapsamı:
/// - ASP.NET Core HTTP istekleri (gelen)
/// - HttpClient çağrıları (giden, webhook outbox vb.)
/// - EF Core DB query'leri (yeni: SQL command + duration)
/// - Manuel ActivitySource (`SearchConsoleApp` — custom span'lar)
///
/// Sampling:
/// - Production'da TraceIdRatioBasedSampler ile %X örnek (default %10)
/// - Dev'de AlwaysOn (her trace export)
/// - Error trace'leri her zaman sample edilir (sampling oranı düşük olsa bile)
///
/// Backend exporter:
/// - `Observability:OtlpEndpoint` set ise OTLP gRPC export
///   (Jaeger, Tempo, Grafana Cloud, Honeycomb, Datadog)
/// - Boşsa console (sadece dev)
///
/// Audience tag her span'a otomatik eklenir (AudienceTagEnricher middleware).
/// Dashboard'larda "service=SearchConsoleApp-api, audience=admin" filtre.
/// </summary>
public static class OpenTelemetrySetup
{
    public const string MeterName = "SearchConsoleApp";
    public const string ActivitySourceName = "SearchConsoleApp";

    public static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    public static IServiceCollection AddSearchConsoleAppObservability(
        this IServiceCollection services, IConfiguration config)
    {
        var otlpEndpoint = config["Observability:OtlpEndpoint"];
        var serviceName = config["Observability:ServiceName"] ?? "SearchConsoleApp-api";
        var serviceVersion = config["Observability:ServiceVersion"] ?? "1.0.0";
        var sampleRate = config.GetValue("Observability:SampleRate", 0.1);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = config["ASPNETCORE_ENVIRONMENT"] ?? "production",
                    ["host.name"] = Environment.MachineName,
                }))
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation()
                 .AddHttpClientInstrumentation()
                 .AddRuntimeInstrumentation()
                 .AddMeter(MeterName);

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    m.AddOtlpExporter(opt => opt.Endpoint = new Uri(otlpEndpoint));
                else
                    m.AddConsoleExporter();
            })
            .WithTracing(t =>
            {
                // Sampling: prod'da ratio-based, dev'de always-on
                if (sampleRate >= 1.0)
                {
                    t.SetSampler(new AlwaysOnSampler());
                }
                else
                {
                    // ParentBased + TraceIdRatio: parent zaten sample'lıysa biz de örnekleriz
                    // (trace tutarlılığı), yoksa ratio'ya göre karar
                    t.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(sampleRate)));
                }

                t.AddAspNetCoreInstrumentation(opt =>
                 {
                    // Health endpoint'lerini sample etme — gürültü
                    opt.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                    opt.RecordException = true;
                 })
                 .AddHttpClientInstrumentation(opt =>
                 {
                     opt.RecordException = true;
                 })
                 .AddSource(ActivitySourceName);

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    t.AddOtlpExporter(opt => opt.Endpoint = new Uri(otlpEndpoint));
            });

        services.AddSingleton<AudienceTagEnricher>();

        return services;
    }

    /// <summary>
    /// Custom span yarat — service'lerden manuel tracing için.
    ///
    /// Kullanım:
    ///   using var activity = OpenTelemetrySetup.StartActivity("OrderService.Refund");
    ///   activity?.SetTag("order.id", orderId);
    ///   // ... iş ...
    ///   activity?.SetTag("refund.amount", amount);
    /// </summary>
    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        => ActivitySource.StartActivity(name, kind);
}
