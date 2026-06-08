using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Core.FeatureFlags;
using SearchConsoleApp.IntegrationTests.Infrastructure;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

public class FeatureFlagsTests : IClassFixture<SearchConsoleAppWebApplicationFactory>
{
    private readonly SearchConsoleAppWebApplicationFactory _factory;

    public FeatureFlagsTests(SearchConsoleAppWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task IsEnabledAsync_returns_default_when_flag_missing()
    {
        using var scope = _factory.Services.CreateScope();
        var flags = scope.ServiceProvider.GetRequiredService<IFeatureFlags>();

        (await flags.IsEnabledAsync("non-existent-flag", defaultValue: false)).Should().BeFalse();
        (await flags.IsEnabledAsync("non-existent-flag", defaultValue: true)).Should().BeTrue();
    }

    [Fact]
    public async Task Simple_boolean_flag_resolves()
    {
        var customFactory = new SearchConsoleAppWebApplicationFactory();
        customFactory.WithCustomConfig(new Dictionary<string, string?>
        {
            ["FeatureFlags:test-simple"] = "true"
        });

        using var scope = customFactory.Services.CreateScope();
        var flags = scope.ServiceProvider.GetRequiredService<IFeatureFlags>();

        (await flags.IsEnabledAsync("test-simple")).Should().BeTrue();
    }

    [Fact]
    public async Task Targeted_flag_enables_for_specific_customer()
    {
        var customFactory = new SearchConsoleAppWebApplicationFactory();
        customFactory.WithCustomConfig(new Dictionary<string, string?>
        {
            ["FeatureFlags:target-test:default"] = "false",
            ["FeatureFlags:target-test:enabledForCustomerIds:0"] = "42",
        });

        using var scope = customFactory.Services.CreateScope();
        var flags = scope.ServiceProvider.GetRequiredService<IFeatureFlags>();

        // Customer 42 → açık
        (await flags.IsEnabledAsync("target-test", false,
            new EvaluationContext { TargetingKey = "42" })).Should().BeTrue();

        // Customer 99 → kapalı (default false)
        (await flags.IsEnabledAsync("target-test", false,
            new EvaluationContext { TargetingKey = "99" })).Should().BeFalse();
    }

    [Fact]
    public async Task MaintenanceMode_returns_503_for_non_admin()
    {
        var customFactory = new SearchConsoleAppWebApplicationFactory();
        customFactory.WithCustomConfig(new Dictionary<string, string?>
        {
            ["FeatureFlags:maintenance-mode"] = "true"
        });

        var client = customFactory.CreateClient();

        // Public endpoint — 503 dönmeli
        var res = await client.PostAsync("/api/v1/public/auth/login", null);
        res.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        res.Headers.TryGetValues("Retry-After", out _).Should().BeTrue();
    }

    [Fact]
    public async Task MaintenanceMode_does_not_block_health_endpoints()
    {
        var customFactory = new SearchConsoleAppWebApplicationFactory();
        customFactory.WithCustomConfig(new Dictionary<string, string?>
        {
            ["FeatureFlags:maintenance-mode"] = "true"
        });

        var client = customFactory.CreateClient();
        var res = await client.GetAsync("/health/live");

        // Health endpoint'leri her zaman çalışmalı — k8s probe'ları
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
