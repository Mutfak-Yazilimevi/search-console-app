using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SearchConsoleApp.IntegrationTests.Infrastructure;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

public class HealthCheckTests : IClassFixture<SearchConsoleAppWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(SearchConsoleAppWebApplicationFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_endpoint_returns_200()
    {
        var res = await _client.GetAsync("/health/live");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_endpoint_returns_json_with_checks()
    {
        var res = await _client.GetAsync("/health");
        res.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var body = await res.Content.ReadFromJsonAsync<HealthResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().BeOneOf("Healthy", "Degraded", "Unhealthy");
        body.Checks.Should().NotBeEmpty();
    }

    private record HealthResponse(string Status, double TotalDuration, List<CheckEntry> Checks);
    private record CheckEntry(string Name, string Status, double Duration, string? Description, List<string> Tags);
}
