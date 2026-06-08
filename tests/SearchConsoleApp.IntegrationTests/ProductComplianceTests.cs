using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Core.Domain.MerchantCenter;
using SearchConsoleApp.Data;
using SearchConsoleApp.IntegrationTests.Infrastructure;
using SearchConsoleApp.Services.MerchantCenter;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

public class ProductComplianceTests : IAsyncLifetime
{
    private SearchConsoleAppWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new SearchConsoleAppWebApplicationFactory().WithCustomConfig(new Dictionary<string, string?>
        {
            ["Audit:WebhookSecret"] = "test-webhook-secret",
            ["Audit:CrawlWorkerUrl"] = "",
            ["RateLimit:Audit:PermitLimit"] = "10000",
        });
        _client = _factory.CreateClient();
        return _factory.ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Integration_status_lists_gmc_relevant_integrations()
    {
        var res = await _client.GetAsync("/api/v1/public/merchant-center/compliance/integrations/status");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadEnvelopeDataAsync<GmcIntegrationStatusResponse>();
        body!.Integrations.Should().Contain(i => i.Id == "crawl-worker");
        body.Integrations.Should().Contain(i => i.Id == "pagespeed");
        body.Integrations.Should().Contain(i => i.Id == "merchant-center-oauth");
    }

    private record GmcIntegrationStatusResponse(List<GmcIntegrationItem> Integrations);
    private record GmcIntegrationItem(string Id, string Label, string Status);

    [Fact]
    public async Task Start_compliance_requires_url()
    {
        var res = await _client.PostAsJsonAsync("/api/v1/public/merchant-center/compliance", new { url = "" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_rejects_missing_secret_when_configured()
    {
        var runId = await SeedCrawlingRunAsync();

        var res = await _client.PostAsJsonAsync("/api/v1/public/merchant-center/compliance/webhook", new
        {
            productComplianceRunEntityId = runId,
            @event = "product",
            url = "https://shop.example.com/urun/1",
            extractedProductJson = MinimalProductJson(),
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_complete_analyzes_products_and_completes_run()
    {
        var runId = await SeedCrawlingRunAsync();

        var productRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/merchant-center/compliance/webhook")
        {
            Content = JsonContent.Create(new
            {
                productComplianceRunEntityId = runId,
                @event = "product",
                url = "https://shop.example.com/urun/1",
                title = "Test Ürün",
                extractedProductJson = MinimalProductJson(),
            }),
        };
        productRequest.Headers.Add("X-Audit-Webhook-Secret", "test-webhook-secret");
        (await _client.SendAsync(productRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        var completeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/merchant-center/compliance/webhook")
        {
            Content = JsonContent.Create(new
            {
                productComplianceRunEntityId = runId,
                @event = "complete",
                totalProducts = 1,
                siteCheckHtml = "<html><body>iade politikası kargo teslimat contact</body></html>",
            }),
        };
        completeRequest.Headers.Add("X-Audit-Webhook-Secret", "test-webhook-secret");
        (await _client.SendAsync(completeRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        var detailRes = await _client.GetAsync($"/api/v1/public/merchant-center/compliance/{runId}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await detailRes.Content.ReadEnvelopeDataAsync<ComplianceDetailDto>();
        detail.Should().NotBeNull();
        detail!.Run.Status.Should().Be("Completed");
        detail.Products.Should().HaveCount(1);
        detail.Run.ComplianceScore.Should().NotBeNull();
    }

    [Fact]
    public async Task Detail_includes_cross_product_issues_for_duplicate_titles()
    {
        var runId = await SeedCrawlingRunAsync();

        await SendWebhookAsync(runId, "product", new
        {
            url = "https://shop.example.com/urun/1",
            title = "Aynı Başlık",
            extractedProductJson = ProductJson("https://shop.example.com/urun/1", "Aynı Başlık"),
        });
        await SendWebhookAsync(runId, "product", new
        {
            url = "https://shop.example.com/urun/2",
            title = "Aynı Başlık",
            extractedProductJson = ProductJson("https://shop.example.com/urun/2", "Aynı Başlık"),
        });
        await SendWebhookAsync(runId, "complete", new { totalProducts = 2, siteCheckHtml = "<html></html>" });

        var detail = await GetDetailAsync(runId);
        detail!.CrossProductIssues.Should().NotBeEmpty();
        detail.CrossProductIssues.Any(i =>
            i.TryGetProperty("ruleId", out var rule) && rule.GetString() == "GMC-X-001")
            .Should().BeTrue();
    }

    [Fact]
    public async Task List_recent_runs_requires_authentication()
    {
        var res = await _client.GetAsync("/api/v1/web/merchant-center/compliance/runs");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_recent_runs_returns_customer_runs()
    {
        await using var factory = new SearchConsoleAppWebApplicationFactory().WithCustomConfig(new Dictionary<string, string?>
        {
            ["RateLimit:Audit:PermitLimit"] = "10000",
        });
        await factory.ResetDatabaseAsync();

        var user = await factory.CreateAuthenticatedUserAsync("gmc-runs@test.local");
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepository<ProductComplianceRun>>();
        await repo.InsertAsync(new ProductComplianceRun
        {
            InputUrl = "https://shop.example.com",
            NormalizedUrl = "https://shop.example.com/",
            Status = ProductComplianceRunStatus.Completed,
            AnalysisMode = ProductComplianceAnalysisMode.SiteOnly,
            CustomerId = user.CustomerId,
            ComplianceScore = 72,
            TotalProducts = 3,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
        }, publishEvent: false);

        var client = factory.AsAuthenticated(user);
        var res = await client.GetAsync("/api/v1/web/merchant-center/compliance/runs?limit=5");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var runs = await res.Content.ReadEnvelopeDataAsync<List<RecentRunDto>>();
        runs.Should().NotBeNull();
        runs!.Should().ContainSingle(r => r.InputUrl == "https://shop.example.com" && r.ComplianceScore == 72);
    }

    [Fact]
    public async Task Export_html_returns_report_for_completed_run()
    {
        var runId = await SeedCrawlingRunAsync();
        await SendWebhookAsync(runId, "product", new
        {
            url = "https://shop.example.com/urun/1",
            extractedProductJson = MinimalProductJson(),
        });
        await SendWebhookAsync(runId, "complete", new { totalProducts = 1, siteCheckHtml = "<html></html>" });

        var res = await _client.GetAsync($"/api/v1/public/merchant-center/compliance/{runId}/export?format=html");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await res.Content.ReadAsStringAsync();
        html.Should().Contain("Merchant Center Ürün Uyumluluk Raporu");
    }

    private async Task<Guid> SeedCrawlingRunAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepository<ProductComplianceRun>>();
        var run = new ProductComplianceRun
        {
            InputUrl = "https://shop.example.com",
            NormalizedUrl = "https://shop.example.com/",
            Status = ProductComplianceRunStatus.Crawling,
            AnalysisMode = ProductComplianceAnalysisMode.SiteOnly,
            CreatedAt = DateTime.UtcNow,
        };
        await repo.InsertAsync(run, publishEvent: false);
        return run.EntityId;
    }

    private async Task SendWebhookAsync(Guid runId, string eventName, object payload)
    {
        var body = new Dictionary<string, object?> { ["productComplianceRunEntityId"] = runId, ["event"] = eventName };
        foreach (var prop in payload.GetType().GetProperties())
            body[prop.Name] = prop.GetValue(payload);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/merchant-center/compliance/webhook")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Audit-Webhook-Secret", "test-webhook-secret");
        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<ComplianceDetailDto?> GetDetailAsync(Guid runId)
    {
        var detailRes = await _client.GetAsync($"/api/v1/public/merchant-center/compliance/{runId}");
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);
        return await detailRes.Content.ReadEnvelopeDataAsync<ComplianceDetailDto>();
    }

    private static string ProductJson(string url, string name) =>
        $$"""
        {
          "url":"{{url}}",
          "name":"{{name}}",
          "sku":"SKU-1",
          "brand":"Marka",
          "image":"https://cdn.example.com/shoe.jpg",
          "images":["https://cdn.example.com/shoe.jpg"],
          "imageCount":1,
          "schemaPrice":999,
          "visiblePrice":999,
          "priceCurrency":"TRY",
          "availability":"InStock",
          "condition":"new",
          "hasProductSchema":true,
          "isHttps":true
        }
        """;

    private static string MinimalProductJson() => ProductJson("https://shop.example.com/urun/1", "Test Ürün");

    private record ComplianceDetailDto(
        ComplianceRunDto Run,
        List<ComplianceProductDto> Products,
        List<System.Text.Json.JsonElement> SiteIssues,
        List<System.Text.Json.JsonElement> CrossProductIssues,
        List<System.Text.Json.JsonElement> FeedIssues);
    private record ComplianceRunDto(string Status, int? ComplianceScore);
    private record ComplianceProductDto(string PageUrl, int IssueCount);
    private record RecentRunDto(string InputUrl, int? ComplianceScore);
}
