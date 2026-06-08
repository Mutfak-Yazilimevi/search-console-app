using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core.Domain.MerchantCenter;

namespace SearchConsoleApp.Services.MerchantCenter;

public partial class ProductComplianceService
{
    private async Task EnqueueCrawlAsync(ProductComplianceRun run, CancellationToken cancellationToken)
    {
        var workerUrl = _config["Audit:CrawlWorkerUrl"];
        if (string.IsNullOrWhiteSpace(workerUrl))
        {
            run.Status = ProductComplianceRunStatus.Failed;
            run.ErrorMessage = "Crawl worker yapılandırılmamış.";
            run.CompletedAt = DateTime.UtcNow;
            await _runRepo.UpdateAsync(run, publishEvent: false);
            return;
        }

        var maxProducts = _config.GetValue("ProductCompliance:MaxProducts", 100);
        var payload = new
        {
            productComplianceRunEntityId = run.EntityId,
            url = run.NormalizedUrl,
            maxProducts,
        };

        var client = _httpClientFactory.CreateClient("audit-crawl");
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{workerUrl.TrimEnd('/')}/enqueue-product-compliance", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await FailCrawlAsync(run.EntityId, "Ürün tarama işi kuyruğa eklenemedi.", cancellationToken);
            return;
        }

        run.Status = ProductComplianceRunStatus.Crawling;
        run.StartedAt = DateTime.UtcNow;
        run.ProgressPhase = "crawling";
        run.ProgressMessage = "Ürün sayfaları taranıyor…";
        await _runRepo.UpdateAsync(run, publishEvent: false);
    }

    private static bool IsTerminal(ProductComplianceRunStatus status) =>
        status is ProductComplianceRunStatus.Completed
            or ProductComplianceRunStatus.Failed
            or ProductComplianceRunStatus.Cancelled;
}
