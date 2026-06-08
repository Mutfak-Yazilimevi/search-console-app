using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using SearchConsoleApp.IntegrationTests.Infrastructure;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

/// <summary>
/// End-to-end auth akışı testleri.
///
/// Her test kendi DB'sini sıfırlar (IAsyncLifetime → ResetDatabaseAsync).
/// TestEmails ile gönderilen email'lerin içeriği doğrulanır.
/// </summary>
public class AuthFlowTests : IClassFixture<SearchConsoleAppWebApplicationFactory>, IAsyncLifetime
{
    private readonly SearchConsoleAppWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthFlowTests(SearchConsoleAppWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_returns_tokens_and_sends_verification_email()
    {
        var res = await _client.PostAsJsonAsync("/api/v1/public/auth/register", new
        {
            email = "alice@test.com",
            password = "Password123!",
            firstName = "Alice"
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadEnvelopeDataAsync<TokensResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be("alice@test.com");

        // Verification email gönderildi mi?
        _factory.TestEmails.Sent.Should().HaveCount(1);
        var email = _factory.TestEmails.Sent[0];
        email.To.Should().Be("alice@test.com");
        email.Subject.Should().Contain("Email");
    }

    [Fact]
    public async Task Register_duplicate_email_returns_409()
    {
        await RegisterAsync("dup@test.com");
        var res = await _client.PostAsJsonAsync("/api/v1/public/auth/register", new
        {
            email = "dup@test.com",
            password = "Password123!"
        });
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_with_correct_password_returns_tokens()
    {
        await RegisterAsync("bob@test.com");
        _factory.TestEmails.Clear();

        var res = await _client.PostAsJsonAsync("/api/v1/public/auth/login", new
        {
            email = "bob@test.com",
            password = "Password123!"
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadEnvelopeDataAsync<TokensResponse>();
        body!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        await RegisterAsync("charlie@test.com");

        var res = await _client.PostAsJsonAsync("/api/v1/public/auth/login", new
        {
            email = "charlie@test.com",
            password = "WrongPassword!"
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EmailVerification_marks_customer_as_confirmed()
    {
        await RegisterAsync("dave@test.com");

        // Email'den token'ı extract et (HTML body içinde URL var)
        var email = _factory.TestEmails.Sent.Single();
        var token = ExtractTokenFromEmail(email.HtmlBody);
        token.Should().NotBeNullOrEmpty();

        // Verify endpoint'i çağır
        var res = await _client.PostAsJsonAsync("/api/v1/public/auth/email/verify",
            new { token });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        // İkinci kez token'ı kullanamazsın (one-time use)
        var res2 = await _client.PostAsJsonAsync("/api/v1/public/auth/email/verify",
            new { token });
        res2.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotPassword_returns_200_even_for_nonexistent_email()
    {
        // Email enumeration koruması: yok olmayan email için bile 200
        var res = await _client.PostAsJsonAsync("/api/v1/public/auth/password/forgot",
            new { email = "ghost@test.com" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        // Hiçbir email gönderilmedi
        _factory.TestEmails.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task PasswordResetFlow_changes_password_and_revokes_sessions()
    {
        // Setup: register + login
        var tokens = await RegisterAsync("eve@test.com");
        _factory.TestEmails.Clear();

        // Forgot password — token üretilir + mail gider
        await _client.PostAsJsonAsync("/api/v1/public/auth/password/forgot",
            new { email = "eve@test.com" });

        var resetEmail = _factory.TestEmails.Sent
            .Single(e => e.Subject.Contains("Şifre"));
        var resetToken = ExtractTokenFromEmail(resetEmail.HtmlBody);

        // Reset password
        var resetRes = await _client.PostAsJsonAsync("/api/v1/public/auth/password/reset",
            new { token = resetToken, newPassword = "NewPassword123!" });
        resetRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Eski şifre artık çalışmaz
        var oldLoginRes = await _client.PostAsJsonAsync("/api/v1/public/auth/login",
            new { email = "eve@test.com", password = "Password123!" });
        oldLoginRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Yeni şifre çalışır
        var newLoginRes = await _client.PostAsJsonAsync("/api/v1/public/auth/login",
            new { email = "eve@test.com", password = "NewPassword123!" });
        newLoginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Eski refresh token revoke edildi
        var refreshRes = await _client.PostAsJsonAsync("/api/v1/public/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        refreshRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_rotates_token()
    {
        var tokens = await RegisterAsync("frank@test.com");

        var res = await _client.PostAsJsonAsync("/api/v1/public/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var newTokens = await res.Content.ReadEnvelopeDataAsync<TokensResponse>();
        newTokens!.RefreshToken.Should().NotBe(tokens.RefreshToken);  // rotation

        // Eski refresh token artık geçersiz
        var oldRes = await _client.PostAsJsonAsync("/api/v1/public/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        oldRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSessions_with_jwt_returns_current_session()
    {
        var tokens = await RegisterAsync("grace@test.com");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var res = await client.GetAsync("/api/v1/web/sessions");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var sessions = await res.Content.ReadEnvelopeDataAsync<List<SessionDto>>();
        sessions.Should().HaveCount(1);
        sessions![0].IsCurrent.Should().BeTrue();  // JWT'deki sid claim eşleşmeli
    }

    [Fact]
    public async Task UnauthenticatedRequest_to_web_returns_401()
    {
        var res = await _client.GetAsync("/api/v1/web/sessions");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // === Helpers ===

    private async Task<TokensResponse> RegisterAsync(string email)
    {
        var res = await _client.PostAsJsonAsync("/api/v1/public/auth/register", new
        {
            email,
            password = "Password123!"
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadEnvelopeDataAsync<TokensResponse>())!;
    }

    private static string? ExtractTokenFromEmail(string htmlBody)
    {
        // Email template'inde URL şu formatta: .../verify-email?token=XXXX veya .../reset-password?token=XXXX
        var match = Regex.Match(htmlBody, @"\?token=([^""\s&<]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }

    // DTOs for deserialization (minimal mirror of actual response)
    private record TokensResponse(
        string AccessToken,
        DateTime AccessTokenExpiresAt,
        string RefreshToken,
        DateTime RefreshTokenExpiresAt,
        UserResponse User);

    private record UserResponse(Guid EntityId, string Email, string? FirstName, string? LastName, List<string> Roles);

    private record SessionDto(
        long Id, long DeviceId, string Audience, string? IpAddress, string? IpCountry,
        string? UserAgent, DateTime StartedUtc, DateTime LastActivityUtc,
        bool IsCurrent, bool IsActive, DateTime? RevokedUtc, string? RevokedReason);
}
