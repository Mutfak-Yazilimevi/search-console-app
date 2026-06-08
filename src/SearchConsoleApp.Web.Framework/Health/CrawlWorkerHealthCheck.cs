using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SearchConsoleApp.Web.Framework.Health;

/// <summary>
/// Crawl worker HTTP /health probe. Yapılandırılmamışsa Healthy döner (opsiyonel servis).
/// </summary>
public class CrawlWorkerHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public CrawlWorkerHealthCheck(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var workerUrl = _config["Audit:CrawlWorkerUrl"];
        if (string.IsNullOrWhiteSpace(workerUrl))
            return HealthCheckResult.Healthy("Crawl worker URL yapılandırılmamış (atlandı).");

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync($"{workerUrl.TrimEnd('/')}/health", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Crawl worker erişilebilir.")
                : HealthCheckResult.Degraded($"Crawl worker HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Crawl worker erişilemiyor.", ex);
        }
    }
}
