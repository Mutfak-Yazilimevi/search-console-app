using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Email;
using SearchConsoleApp.Services.Identity;

namespace SearchConsoleApp.IntegrationTests.Infrastructure;

/// <summary>
/// Tüm integration test'lerin paylaştığı host fixture.
///
/// - DB: InMemory (her test class'ı için izole)
/// - Email: in-memory sender (gönderilen mesajlar test içinden okunabilir)
/// - GeoIP: no-op (DB dosyası gerekmez)
/// - Rate limit: bypass (test'lerde 429 görmesin)
///
/// `WithWebHostBuilder` ile her test class'ı kendi service override'ını
/// yapabilir, ama default'lar testlerin %90'ında yeterlidir.
/// </summary>
public class SearchConsoleAppWebApplicationFactory : WebApplicationFactory<Program>
{
    public TestEmailSender TestEmails { get; } = new();

    private Dictionary<string, string?>? _customConfig;
    private bool _useFakeCrawlWorker;

    /// <summary>
    /// Crawl worker URL'si ayarlanır ve HTTP istekleri sahte handler ile yanıtlanır.
    /// </summary>
    public SearchConsoleAppWebApplicationFactory WithFakeCrawlWorker()
    {
        _useFakeCrawlWorker = true;
        _customConfig ??= new Dictionary<string, string?>();
        _customConfig["Audit:CrawlWorkerUrl"] = "http://fake-crawl-worker.local";
        return this;
    }

    // SQLite in-memory: bağlantı açık kaldığı sürece DB yaşar. EF InMemory
    // provider'ı ExecuteUpdate'i ve unique index gibi ilişkisel kısıtları
    // desteklemediği için SQLite kullanılıyor (production'a daha yakın davranış).
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    /// <summary>
    /// Test başına config override. Fluent — yeni factory instance üretirken
    /// veya CreateClient öncesi çağrılır.
    /// </summary>
    public SearchConsoleAppWebApplicationFactory WithCustomConfig(Dictionary<string, string?> overrides)
    {
        _customConfig = overrides;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Default appsettings'i InMemory ile override et
            var defaults = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TEST_jwt_key_at_least_32_chars_long_for_xunit_runs!!",
                ["Jwt:Issuer"] = "SearchConsoleAppTest",
                ["Jwt:Audience"] = "SearchConsoleAppTestClients",
                ["App:Name"] = "SearchConsoleAppTest",
                ["App:PublicUrl"] = "http://localhost",
                ["Email:Mode"] = "test",
                ["Cache:Provider"] = "memory",
                ["Audit:CaptureChanges"] = "true",
                ["RateLimit:Public:PermitLimit"] = "10000",
                ["RateLimit:Auth:PermitLimit"] = "10000",
                ["RateLimit:Audit:PermitLimit"] = "10000",
                ["Audit:CrawlWorkerUrl"] = "",
                ["Realtime:Enabled"] = "false",   // SignalR kapalı — test'te gereksiz
                ["Outbox:PollIntervalSeconds"] = "60",  // test sırasında dispatch yapma
            };
            config.AddInMemoryCollection(defaults);

            // Test-specific overrides en sonda — daha yüksek priority
            if (_customConfig != null)
            {
                config.AddInMemoryCollection(_customConfig);
            }
        });

        builder.ConfigureServices(services =>
        {
            // EF: production DbContext kaydını sil, InMemory ile değiştir.
            // EF Core 9'da AddDbContext yalnızca DbContextOptions<T> değil,
            // IDbContextOptionsConfiguration<T> kaydı da yapar. Sadece
            // DbContextOptions silinirse SqlServer konfigürasyonu kalır ve
            // "Only a single database provider can be registered" hatası alınır.
            var efDescriptors = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<SearchConsoleAppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(SearchConsoleAppDbContext) ||
                (d.ServiceType.IsGenericType &&
                 d.ServiceType.GetGenericTypeDefinition().Name == "IDbContextOptionsConfiguration`1")
            ).ToList();
            foreach (var d in efDescriptors) services.Remove(d);

            _connection.Open();
            services.AddDbContext<SearchConsoleAppDbContext>(opt =>
                opt.UseSqlite(_connection));

            // Email: TestEmailSender (singleton, fixture içinden inspect edilir)
            var emailDescriptors = services
                .Where(d => d.ServiceType == typeof(IEmailSender)).ToList();
            foreach (var d in emailDescriptors) services.Remove(d);
            services.AddSingleton<IEmailSender>(TestEmails);

            // GeoIP: NoOp (test'te DB dosyası yok)
            var geoIpDescriptors = services
                .Where(d => d.ServiceType == typeof(IGeoIpService)).ToList();
            foreach (var d in geoIpDescriptors) services.Remove(d);
            services.AddSingleton<IGeoIpService, NoOpGeoIpService>();
        });

        builder.ConfigureTestServices(services =>
        {
            if (_useFakeCrawlWorker)
            {
                services.AddHttpClient("audit-crawl")
                    .ConfigurePrimaryHttpMessageHandler(() => new FakeCrawlWorkerHandler());
            }
        });
    }

    /// <summary>DB'yi temizle (her test başında çağrılır).</summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.EnsureCreatedAsync();
        TestEmails.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}

/// <summary>Test sırasında gönderilen email'leri biriktirir.</summary>
public class TestEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = new();
    private readonly object _lock = new();

    public IReadOnlyList<EmailMessage> Sent
    {
        get { lock (_lock) { return _sent.ToList(); } }
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        lock (_lock) { _sent.Add(message); }
        return Task.CompletedTask;
    }

    public void Clear() { lock (_lock) { _sent.Clear(); } }
}

public class NoOpGeoIpService : IGeoIpService
{
    public GeoIpResult? Lookup(string? ipAddress) => null;
}
