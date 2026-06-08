using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Domain.Auditing;
using SearchConsoleApp.Data;
using SearchConsoleApp.IntegrationTests.Infrastructure;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

public class GdprTests : IClassFixture<SearchConsoleAppWebApplicationFactory>, IAsyncLifetime
{
    private readonly SearchConsoleAppWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GdprTests(SearchConsoleAppWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task User_can_export_own_data_as_json()
    {
        var token = await RegisterAndLoginAsync("alice@gdpr.test");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.GetAsync("/api/v1/web/account/privacy/export");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
        var json = await res.Content.ReadAsStringAsync();

        // Export içeriği customer + audit + session içermeli
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("customer").GetProperty("email").GetString()
            .Should().Be("alice@gdpr.test");
        doc.RootElement.TryGetProperty("auditLogs", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("sessions", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("devices", out _).Should().BeTrue();

        // PasswordHash GİBİ hassas veri export'ta OLMAMALI
        json.Should().NotContain("passwordHash", "PII export'ta credential olmaz");
        json.Should().NotContain("totpSecret", "secret export'ta olmaz");
    }

    [Fact]
    public async Task Self_delete_anonymizes_customer_and_preserves_audit_action()
    {
        var token = await RegisterAndLoginAsync("bob@gdpr.test");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        long customerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();
            customerId = ctx.Set<Customer>().Single(c => c.Email == "bob@gdpr.test").Id;
        }

        // Delete request
        var res = await _client.PostAsJsonAsync("/api/v1/web/account/privacy/delete", new
        {
            password = "Password123!",
            reason = "test"
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        // DB durumunu doğrula
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();

            // Soft-delete + PII anonymize
            // (global query filter Deleted=false öğeleri gizliyor — IgnoreQueryFilters)
            var customer = ctx.Set<Customer>().IgnoreQueryFilters().Single(c => c.Id == customerId);
            customer.Deleted.Should().BeTrue();
            customer.Active.Should().BeFalse();
            customer.Email.Should().StartWith("deleted-");
            customer.Email.Should().Contain("@anonymized");
            customer.FirstName.Should().BeNull();
            customer.PasswordHash.Should().BeNull();

            // AuditLog'larda Action korunmuş, ActorEmail temizlenmiş
            var auditLogs = ctx.Set<AuditLog>()
                .Where(a => a.ActorCustomerId == customerId)
                .ToList();
            auditLogs.Should().NotBeEmpty("audit kayıtları korunmalı");
            auditLogs.Should().AllSatisfy(a =>
            {
                a.ActorEmail.Should().BeNull("PII anonymize edilmeli");
                a.ActorIp.Should().BeNull();
                a.Action.Should().NotBeNullOrEmpty("ne yapıldığı hukuki kayıt");
            });

            // gdpr.anonymize action'ı eklenmiş olmalı
            ctx.Set<AuditLog>()
                .Any(a => a.Action == "gdpr.anonymize" && a.TargetId == customerId)
                .Should().BeTrue("delete işleminin kendisi audit'lenmeli");
        }
    }

    [Fact]
    public async Task Self_delete_with_wrong_password_fails()
    {
        var token = await RegisterAndLoginAsync("charlie@gdpr.test");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _client.PostAsJsonAsync("/api/v1/web/account/privacy/delete", new
        {
            password = "WrongPassword!",
            reason = "test"
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deleted_customer_cannot_login()
    {
        var token = await RegisterAndLoginAsync("dave@gdpr.test");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await _client.PostAsJsonAsync("/api/v1/web/account/privacy/delete", new
        {
            password = "Password123!",
            reason = "test"
        });

        // Aynı email ile login dene → 401
        var login = await _client.PostAsJsonAsync("/api/v1/public/auth/login", new
        {
            email = "dave@gdpr.test",
            password = "Password123!"
        });
        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<string> RegisterAndLoginAsync(string email)
    {
        var reg = await _client.PostAsJsonAsync("/api/v1/public/auth/register", new
        {
            email,
            password = "Password123!"
        });
        reg.EnsureSuccessStatusCode();
        var tokens = await reg.Content.ReadEnvelopeDataAsync<TokensResponse>();
        return tokens!.AccessToken;
    }

    private record TokensResponse(string AccessToken);
}
