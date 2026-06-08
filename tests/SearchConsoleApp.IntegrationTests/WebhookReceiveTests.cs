using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using SearchConsoleApp.IntegrationTests.Infrastructure;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

/// <summary>
/// Webhook receiver controller testleri.
///
/// İmza doğrulama + inbox idempotency endpoint-level entegrasyonu test eder.
/// </summary>
public class WebhookReceiveTests : IClassFixture<SearchConsoleAppWebApplicationFactory>, IAsyncLifetime
{
    private const string TestSecret = "test_webhook_signing_secret_at_least_32_chars";

    private readonly SearchConsoleAppWebApplicationFactory _factory;

    public WebhookReceiveTests(SearchConsoleAppWebApplicationFactory factory)
    {
        _factory = factory;
        // Test config — secret set et ki signature doğrulama çalışsın
        factory.WithCustomConfig(new Dictionary<string, string?>
        {
            ["Webhooks:Receive:stripe:SigningSecret"] = TestSecret,
        });
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Webhook_without_signature_returns_401()
    {
        var client = _factory.CreateClient();
        var payload = "{\"type\":\"test\"}";

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Webhook-Event-Id", "evt_no_sig");

        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_with_valid_signature_succeeds()
    {
        var client = _factory.CreateClient();
        var payload = "{\"type\":\"charge.succeeded\",\"amount\":1000}";
        var signature = ComputeSignature(payload, TestSecret);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Webhook-Event-Id", "evt_valid");
        req.Headers.Add("X-Webhook-Signature", signature);

        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Webhook_with_invalid_signature_returns_401()
    {
        var client = _factory.CreateClient();
        var payload = "{\"type\":\"test\"}";

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Webhook-Event-Id", "evt_bad_sig");
        req.Headers.Add("X-Webhook-Signature", "sha256=fakehash");

        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Duplicate_webhook_returns_200_idempotent()
    {
        var client = _factory.CreateClient();
        var payload = "{\"type\":\"charge.succeeded\"}";
        var signature = ComputeSignature(payload, TestSecret);

        async Task<HttpResponseMessage> Send()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/webhooks/stripe")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("X-Webhook-Event-Id", "evt_duplicate");
            req.Headers.Add("X-Webhook-Signature", signature);
            return await client.SendAsync(req);
        }

        // İlk istek: yeni event, 200
        var first = await Send();
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadAsStringAsync();
        firstBody.Should().NotContain("duplicate");

        // İkinci aynı istek: idempotent — yine 200 ama duplicate flag
        var second = await Send();
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadAsStringAsync();
        secondBody.Should().Contain("duplicate");
    }

    [Fact]
    public async Task Webhook_without_event_id_returns_400()
    {
        var client = _factory.CreateClient();
        var payload = "{\"type\":\"test\"}";
        var signature = ComputeSignature(payload, TestSecret);

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        // X-Webhook-Event-Id YOK
        req.Headers.Add("X-Webhook-Signature", signature);

        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
