using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SearchConsoleApp.IntegrationTests.Infrastructure;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

public class AuditTests : IClassFixture<SearchConsoleAppWebApplicationFactory>, IAsyncLifetime
{
    private readonly SearchConsoleAppWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuditTests(SearchConsoleAppWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Start_audit_creates_run_and_returns_entity_id()
    {
        var res = await _client.PostAsJsonAsync("/api/v1/public/audit", new { url = "https://example.com" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadEnvelopeDataAsync<AuditRunDto>();
        body.Should().NotBeNull();
        body!.EntityId.Should().NotBeEmpty();
        body.InputUrl.Should().Be("https://example.com");
        // Crawl worker URL boşsa enqueue başarısız olur ve run Failed döner.
        body.Status.Should().BeOneOf("Pending", "Failed");
    }

    [Fact]
    public async Task Webhook_rejects_missing_secret_when_configured()
    {
        await using var factory = new SearchConsoleAppWebApplicationFactory().WithCustomConfig(new Dictionary<string, string?>
        {
            ["Audit:WebhookSecret"] = "test-webhook-secret",
            ["Audit:CrawlWorkerUrl"] = "",
            ["RateLimit:Audit:PermitLimit"] = "10000",
        });
        var client = factory.CreateClient();
        await factory.ResetDatabaseAsync();

        var start = await client.PostAsJsonAsync("/api/v1/public/audit", new { url = "https://example.com" });
        var run = (await start.Content.ReadEnvelopeDataAsync<AuditRunDto>())!;

        var res = await client.PostAsJsonAsync("/api/v1/public/audit/webhook", new
        {
            auditRunEntityId = run.EntityId,
            @event = "page",
            url = "https://example.com/",
            statusCode = 200,
            crawlDepth = 0,
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_accepts_valid_secret_and_records_page()
    {
        await using var factory = new SearchConsoleAppWebApplicationFactory()
            .WithCustomConfig(new Dictionary<string, string?>
            {
                ["Audit:WebhookSecret"] = "test-webhook-secret",
                ["RateLimit:Audit:PermitLimit"] = "10000",
            })
            .WithFakeCrawlWorker();
        var client = factory.CreateClient();
        await factory.ResetDatabaseAsync();

        var start = await client.PostAsJsonAsync("/api/v1/public/audit", new { url = "https://example.com" });
        var run = (await start.Content.ReadEnvelopeDataAsync<AuditRunDto>())!;

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/audit/webhook")
        {
            Content = JsonContent.Create(new
            {
                auditRunEntityId = run.EntityId,
                @event = "page",
                url = "https://example.com/",
                statusCode = 200,
                title = "Example",
                crawlDepth = 0,
                responseTimeMs = 120,
            }),
        };
        request.Headers.Add("X-Audit-Webhook-Secret", "test-webhook-secret");

        var webhookRes = await client.SendAsync(request);
        webhookRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await client.GetAsync($"/api/v1/public/audit/{run.EntityId}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var audit = (await detail.Content.ReadEnvelopeDataAsync<AuditDetailDto>())!;
        audit.Pages.Should().ContainSingle(p => p.Url == "https://example.com/");
    }

    [Fact]
    public async Task Start_audit_returns_429_when_global_quota_exceeded()
    {
        await using var factory = new SearchConsoleAppWebApplicationFactory().WithCustomConfig(new Dictionary<string, string?>
        {
            ["Audit:Quota:MaxConcurrentGlobal"] = "0",
            ["Audit:CrawlWorkerUrl"] = "",
            ["RateLimit:Audit:PermitLimit"] = "10000",
        });
        var client = factory.CreateClient();
        await factory.ResetDatabaseAsync();

        var res = await client.PostAsJsonAsync("/api/v1/public/audit", new { url = "https://example.com" });
        res.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Health_endpoint_includes_crawl_worker_check()
    {
        var res = await _client.GetAsync("/health");
        res.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var body = await res.Content.ReadFromJsonAsync<HealthResponse>();
        body!.Checks.Should().Contain(c => c.Name == "crawl-worker");
    }

    [Fact]
    public async Task Integration_status_lists_configurable_integrations()
    {
        var res = await _client.GetAsync("/api/v1/public/audit/integrations/status");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await res.Content.ReadEnvelopeDataAsync<IntegrationStatusResponse>();
        body!.Integrations.Should().Contain(i => i.Id == "pagespeed");
        body.Integrations.Should().Contain(i => i.Id == "gemini" && i.CanToggle);
        body.Integrations.First(i => i.Id == "crawl-worker").CanToggle.Should().BeFalse();
    }

    [Fact]
    public async Task Update_integration_persists_api_key_and_toggle()
    {
        var patch = await _client.PatchAsJsonAsync("/api/v1/public/audit/integrations/pagespeed", new
        {
            enabled = true,
            values = new Dictionary<string, string>
            {
                ["Google:PageSpeedApiKey"] = "test-pagespeed-key",
            },
        });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await patch.Content.ReadEnvelopeDataAsync<IntegrationItemResponse>();
        updated!.Status.Should().Be("configured");
        updated.Fields.Should().Contain(f => f.Key == "Google:PageSpeedApiKey" && f.HasValue);

        var disable = await _client.PatchAsJsonAsync("/api/v1/public/audit/integrations/gemini", new { enabled = false });
        disable.StatusCode.Should().Be(HttpStatusCode.OK);
        var disabled = await disable.Content.ReadEnvelopeDataAsync<IntegrationItemResponse>();
        disabled!.Enabled.Should().BeFalse();
        disabled.Status.Should().Be("disabled");
    }

    private record IntegrationStatusResponse(List<IntegrationItemResponse> Integrations);
    private record IntegrationItemResponse(
        string Id,
        string Label,
        string Status,
        bool Enabled,
        bool CanToggle,
        List<IntegrationFieldResponse> Fields);
    private record IntegrationFieldResponse(string Key, string Label, bool IsSecret, bool HasValue);

    private record AuditRunDto(
        Guid EntityId,
        string InputUrl,
        string NormalizedUrl,
        string Status,
        string Mode);

    private record AuditDetailDto(AuditRunDto Run, List<ScannedPageDto> Pages, List<object> Issues);
    private record ScannedPageDto(Guid EntityId, string Url, int? StatusCode, string? Title);

    private record HealthResponse(string Status, double TotalDuration, List<CheckEntry> Checks);
    private record CheckEntry(string Name, string Status, double Duration, string? Description, List<string> Tags);
}
