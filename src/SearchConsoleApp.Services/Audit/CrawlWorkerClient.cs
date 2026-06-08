using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Audit;

public interface ICrawlWorkerClient
{
    Task CancelJobAsync(Guid auditRunEntityId, CancellationToken cancellationToken = default);
    Task CancelProductComplianceJobAsync(Guid runEntityId, CancellationToken cancellationToken = default);
}

public partial class CrawlWorkerClient : ICrawlWorkerClient, IScopedService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CrawlWorkerClient> _logger;

    public CrawlWorkerClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<CrawlWorkerClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task CancelJobAsync(Guid auditRunEntityId, CancellationToken cancellationToken = default)
    {
        var workerUrl = _config["Audit:CrawlWorkerUrl"];
        if (string.IsNullOrWhiteSpace(workerUrl))
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("audit-crawl");
            var json = JsonSerializer.Serialize(new { auditRunEntityId });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{workerUrl.TrimEnd('/')}/cancel", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Crawl worker cancel returned {Status} for {EntityId}: {Body}",
                    (int)response.StatusCode, auditRunEntityId, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call crawl worker cancel for {EntityId}", auditRunEntityId);
        }
    }

    public async Task CancelProductComplianceJobAsync(Guid runEntityId, CancellationToken cancellationToken = default)
    {
        var workerUrl = _config["Audit:CrawlWorkerUrl"];
        if (string.IsNullOrWhiteSpace(workerUrl))
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("audit-crawl");
            var json = JsonSerializer.Serialize(new { productComplianceRunEntityId = runEntityId });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{workerUrl.TrimEnd('/')}/cancel", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Crawl worker product-compliance cancel returned {Status} for {EntityId}: {Body}",
                    (int)response.StatusCode, runEntityId, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call crawl worker cancel for product compliance {EntityId}", runEntityId);
        }
    }
}
