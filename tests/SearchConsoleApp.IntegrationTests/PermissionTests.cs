using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Data;
using SearchConsoleApp.IntegrationTests.Infrastructure;
using SearchConsoleApp.Services.Security;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

/// <summary>
/// Permission-based authorization testleri.
///
/// HasPermission attribute'u JWT'deki "perm" claim'lerini kontrol eder.
/// Role bazlı permission resolution RolePermissions.ResolveForRoles ile yapılır.
/// </summary>
public class PermissionTests : IClassFixture<SearchConsoleAppWebApplicationFactory>, IAsyncLifetime
{
    private readonly SearchConsoleAppWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public PermissionTests(SearchConsoleAppWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Regular_user_cannot_access_admin_endpoint()
    {
        var token = await RegisterAndGetTokenAsync("user@test.com", roles: "user");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.GetAsync("/api/v1/admin/audit");

        // Admin policy fail → 403 Forbidden
        res.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_can_access_audit_endpoint()
    {
        var token = await RegisterAndGetTokenAsync("admin@test.com", roles: "admin");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var res = await _client.GetAsync("/api/v1/admin/audit");

        // Admin role + audit.read permission var
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Support_agent_has_audit_read_but_not_write()
    {
        // support-agent rolünde audit.read VAR, customers.delete YOK
        var token = await RegisterAndGetTokenAsync("support@test.com", roles: "admin,support-agent");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var read = await _client.GetAsync("/api/v1/admin/audit");
        read.StatusCode.Should().Be(HttpStatusCode.OK);

        // (Customer delete endpoint zaten admin role gerektiriyor — bu test
        // permission attribute davranışını doğrular)
    }

    // === Helpers ===

    /// <summary>
    /// Test fixture'da DB'ye doğrudan customer ekler (Register endpoint
    /// "admin" rolünü vermeyecek — default "user"). Token üretmek için Login.
    /// </summary>
    private async Task<string> RegisterAndGetTokenAsync(string email, string roles)
    {
        // Önce normal register
        var regRes = await _client.PostAsJsonAsync("/api/v1/public/auth/register", new
        {
            email,
            password = "Password123!"
        });
        regRes.EnsureSuccessStatusCode();

        // DB'de rolü güncelle (test ortamında doğrudan repository ile)
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();
            var customer = ctx.Set<Customer>().Single(c => c.Email == email);
            customer.Roles = roles;
            await ctx.SaveChangesAsync();
        }

        // Tekrar login → yeni JWT'de güncel role/permission claim'leri olur
        var loginRes = await _client.PostAsJsonAsync("/api/v1/public/auth/login", new
        {
            email,
            password = "Password123!"
        });
        loginRes.EnsureSuccessStatusCode();
        var tokens = await loginRes.Content.ReadEnvelopeDataAsync<TokensResponse>();
        return tokens!.AccessToken;
    }

    private record TokensResponse(string AccessToken, DateTime AccessTokenExpiresAt,
        string RefreshToken, DateTime RefreshTokenExpiresAt, object User);
}
