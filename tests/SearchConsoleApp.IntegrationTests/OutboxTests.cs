using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Core.Domain.Outbox;
using SearchConsoleApp.Data;
using SearchConsoleApp.IntegrationTests.Infrastructure;
using SearchConsoleApp.Services.Outbox;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

/// <summary>
/// Outbox pattern testleri.
///
/// Dispatcher background worker test sırasında çalışmıyor — manuel
/// `ProcessBatchAsync` çağıramayız (private). Onun yerine:
/// 1. Enqueue testleri — DB'ye doğru yazılıyor mu
/// 2. Handler testleri — WebhookOutboxHandler doğru davranıyor mu
/// 3. End-to-end (manuel dispatch) — service-level helper ile
/// </summary>
public class OutboxTests : IClassFixture<SearchConsoleAppWebApplicationFactory>, IAsyncLifetime
{
    private readonly SearchConsoleAppWebApplicationFactory _factory;

    public OutboxTests(SearchConsoleAppWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EnqueueAsync_writes_pending_message()
    {
        using var scope = _factory.Services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        var ctx = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();

        await outbox.EnqueueAsync(new OutboxEnqueue
        {
            MessageType = "webhook.test.created",
            Target = "https://example.com/hook",
            Payload = "{\"foo\":\"bar\"}",
            Headers = new() { ["X-Custom"] = "value" }
        });

        var msg = await ctx.Set<OutboxMessage>().SingleAsync();
        msg.MessageType.Should().Be("webhook.test.created");
        msg.Target.Should().Be("https://example.com/hook");
        msg.Payload.Should().Be("{\"foo\":\"bar\"}");
        msg.Status.Should().Be("pending");
        msg.AttemptCount.Should().Be(0);
        msg.HeadersJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task WebhookHandler_CanHandle_only_webhook_prefix()
    {
        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IOutboxMessageHandler>()
            .OfType<WebhookOutboxHandler>()
            .Single();

        handler.CanHandle("webhook.order.created").Should().BeTrue();
        handler.CanHandle("Webhook.Order.Created").Should().BeTrue();   // case-insensitive
        handler.CanHandle("broker.OrderCreated").Should().BeFalse();
        handler.CanHandle("random").Should().BeFalse();
    }

    [Fact(Skip = "Requires network — httpbin.org. Enable for local manual run.")]
    public async Task WebhookHandler_throws_permanent_on_4xx()
    {
        // 4xx → OutboxPermanentException (retry yok, dead'e)
        using var scope = _factory.Services.CreateScope();
        var handler = scope.ServiceProvider.GetServices<IOutboxMessageHandler>()
            .OfType<WebhookOutboxHandler>()
            .Single();

        var msg = new OutboxMessage
        {
            MessageType = "webhook.test",
            // httpbin.org/status/422 → 422 döner
            Target = "https://httpbin.org/status/422",
            Payload = "{}",
            EntityId = Guid.NewGuid(),
        };

        // NOT: Bu test gerçek network çağrısı yapar — CI'da skip etmek için
        // [Trait("Category", "Network")] eklenebilir
        var act = () => handler.SendAsync(msg, CancellationToken.None);
        await act.Should().ThrowAsync<OutboxPermanentException>();
    }

    [Fact]
    public async Task Admin_can_list_outbox_messages()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            await outbox.EnqueueAsync(new OutboxEnqueue
            {
                MessageType = "webhook.foo",
                Target = "https://test",
                Payload = "{}"
            });
        }

        var admin = await _factory.CreateAuthenticatedUserAsync("admin@out.test", roles: "admin");
        var client = _factory.AsAuthenticated(admin);

        var res = await client.GetAsync("/api/v1/admin/outbox");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("webhook.foo");
    }
}
