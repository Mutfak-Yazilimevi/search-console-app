using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.IntegrationTests.Infrastructure;
using SearchConsoleApp.Services.Inbox;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

/// <summary>
/// Inbox pattern idempotency testleri.
///
/// Kritik garanti: aynı (Source, ExternalEventId) iki kez kaydedilemez.
/// Unique constraint DB-level koruma sağlar.
/// </summary>
public class InboxTests : IClassFixture<SearchConsoleAppWebApplicationFactory>, IAsyncLifetime
{
    private readonly SearchConsoleAppWebApplicationFactory _factory;

    public InboxTests(SearchConsoleAppWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task First_record_returns_new_then_duplicate_returns_already_processed()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();

        var first = await inbox.TryRecordAsync(
            source: "stripe",
            externalEventId: "evt_123",
            eventType: "charge.succeeded",
            payload: "{\"amount\":1000}");

        first.AlreadyProcessed.Should().BeFalse();
        first.Id.Should().BeGreaterThan(0);

        // İkinci aynı event — duplicate
        var second = await inbox.TryRecordAsync(
            source: "stripe",
            externalEventId: "evt_123",
            eventType: "charge.succeeded",
            payload: "{\"amount\":1000}");

        second.AlreadyProcessed.Should().BeTrue();
        second.Id.Should().Be(first.Id);  // aynı kayıt
    }

    [Fact]
    public async Task Different_sources_with_same_event_id_are_separate()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();

        var stripe = await inbox.TryRecordAsync("stripe", "evt_123", "type1", "{}");
        var github = await inbox.TryRecordAsync("github", "evt_123", "type1", "{}");

        // Aynı ExternalEventId ama farklı Source — iki ayrı kayıt
        stripe.AlreadyProcessed.Should().BeFalse();
        github.AlreadyProcessed.Should().BeFalse();
        stripe.Id.Should().NotBe(github.Id);
    }

    [Fact]
    public async Task MarkProcessed_updates_status()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
        var ctx = scope.ServiceProvider.GetRequiredService<SearchConsoleApp.Data.SearchConsoleAppDbContext>();

        var result = await inbox.TryRecordAsync("stripe", "evt_proc", "t", "{}");
        await inbox.MarkProcessedAsync(result.Id);

        // ExecuteUpdateAsync DB'yi doğrudan günceller, scope'taki tracker'ı değil.
        // Aynı context'ten tracked okuma stale döner — AsNoTracking ile DB'den oku.
        var entity = await ctx.Set<Core.Domain.Inbox.InboxMessage>()
            .AsNoTracking()
            .FirstAsync(m => m.Id == result.Id);
        entity.Status.Should().Be("processed");
        entity.ProcessedUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkFailed_truncates_long_error()
    {
        using var scope = _factory.Services.CreateScope();
        var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
        var ctx = scope.ServiceProvider.GetRequiredService<SearchConsoleApp.Data.SearchConsoleAppDbContext>();

        var result = await inbox.TryRecordAsync("stripe", "evt_fail", "t", "{}");

        // 3000 karakterlik hata mesajı — 2000'e truncate olmalı
        var longError = new string('x', 3000);
        await inbox.MarkFailedAsync(result.Id, longError);

        // ExecuteUpdateAsync DB'yi doğrudan günceller, scope'taki tracker'ı değil.
        // Aynı context'ten tracked okuma stale döner — AsNoTracking ile DB'den oku.
        var entity = await ctx.Set<Core.Domain.Inbox.InboxMessage>()
            .AsNoTracking()
            .FirstAsync(m => m.Id == result.Id);
        entity.Status.Should().Be("failed");
        entity.Error!.Length.Should().BeLessOrEqualTo(2000);
    }
}
