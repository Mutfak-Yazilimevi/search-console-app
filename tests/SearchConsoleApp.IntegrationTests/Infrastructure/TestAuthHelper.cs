using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.IntegrationTests.Infrastructure;

/// <summary>
/// Test'lerde tekrarlanan auth setup adımları için helper.
///
/// Pattern: register → DB'de role override → login. Register endpoint
/// "admin" rolünü doğrudan vermiyor (güvenlik), test fixture içinden
/// rol güncellenir.
/// </summary>
public static class TestAuthHelper
{
    public record TestUser(string AccessToken, long CustomerId, string Email);

    /// <summary>
    /// Yeni customer yarat (verilen role ile), login ol, access token döner.
    /// </summary>
    public static async Task<TestUser> CreateAuthenticatedUserAsync(
        this SearchConsoleAppWebApplicationFactory factory,
        string email,
        string roles = "user",
        string password = "Password123!")
    {
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/public/auth/register", new
        {
            email,
            password
        });

        long customerId;
        using (var scope = factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();
            var customer = ctx.Set<Customer>().Single(c => c.Email == email);
            customer.Roles = roles;
            await ctx.SaveChangesAsync();
            customerId = customer.Id;
        }

        // Tekrar login → güncel role'larla yeni JWT
        var login = await client.PostAsJsonAsync("/api/v1/public/auth/login", new
        {
            email,
            password
        });
        login.EnsureSuccessStatusCode();
        var tokens = await login.Content.ReadEnvelopeDataAsync<TokensResponse>();

        return new TestUser(tokens!.AccessToken, customerId, email);
    }

    /// <summary>Authenticated client — Authorization header set edilmiş.</summary>
    public static HttpClient AsAuthenticated(this SearchConsoleAppWebApplicationFactory factory, TestUser user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        return client;
    }

    private record TokensResponse(string AccessToken);
}
