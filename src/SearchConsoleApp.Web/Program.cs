using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Serilog;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Auth;
using SearchConsoleApp.Web.Framework.Auth;
using SearchConsoleApp.Web.Framework.Caching;
using SearchConsoleApp.Web.Framework.Email;
using SearchConsoleApp.Web.Framework.Filters;
using SearchConsoleApp.Web.Framework.Health;
using SearchConsoleApp.Web.Framework.Infrastructure;
using SearchConsoleApp.Web.Framework.Logging;
using SearchConsoleApp.Web.Framework.Observability;
using SearchConsoleApp.Web.Framework.Localization;
using SearchConsoleApp.Web.Framework.RateLimiting;
using SearchConsoleApp.Web.Framework.Realtime;
using SearchConsoleApp.Web.Framework.Storage;
using SearchConsoleApp.Web.Framework.Versioning;

var builder = WebApplication.CreateBuilder(args);

var integrationOverridesPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "integration-overrides.json");
Directory.CreateDirectory(Path.GetDirectoryName(integrationOverridesPath)!);
builder.Configuration.AddJsonFile(integrationOverridesPath, optional: true, reloadOnChange: true);

// === Logging ===
builder.UseSearchConsoleAppSerilog();

// === Database ===
builder.Services.AddDbContext<SearchConsoleAppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped(typeof(SearchConsoleApp.Data.IRepository<>), typeof(SearchConsoleApp.Data.EfRepository<>));

// === HttpClient ===
builder.Services.AddHttpClient("expo-push", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
    c.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddHttpContextAccessor();

// === Cache ===
builder.Services.AddSearchConsoleAppCache(builder.Configuration);

// === Email (smtp veya log, config'e göre) ===
builder.Services.AddSearchConsoleAppEmail(builder.Configuration);

// === Observability ===
builder.Services.AddSearchConsoleAppObservability(builder.Configuration);

// === Health checks ===
builder.Services.AddSearchConsoleAppHealthChecks(builder.Configuration);

// === Rate limiting ===
builder.Services.AddSearchConsoleAppRateLimiting(builder.Configuration);

// === Blob storage ===
builder.Services.AddSearchConsoleAppBlobStorage(builder.Configuration);

// === Realtime (SignalR) ===
builder.Services.AddSearchConsoleAppRealtime(builder.Configuration);

// === Memory cache (OAuth state, geçici token'lar) ===
builder.Services.AddMemoryCache();

// === Auto-DI (Scrutor) ===
builder.Services.AddSearchConsoleAppServices(
    typeof(SearchConsoleApp.Services.Customers.CustomerService).Assembly,
    typeof(SearchConsoleApp.Web.Framework.Auth.JwtIssuer).Assembly
);

// === Background jobs ===
builder.Services.AddHostedService<RefreshTokenCleanupService>();
builder.Services.AddHostedService<SearchConsoleApp.Services.Auditing.AuditCleanupService>();
builder.Services.AddHostedService<SearchConsoleApp.Services.Outbox.OutboxDispatcherService>();
builder.Services.AddHostedService<SearchConsoleApp.Services.Outbox.OutboxCleanupService>();
builder.Services.AddHostedService<SearchConsoleApp.Services.Audit.ScheduledAuditWorker>();
builder.Services.AddHostedService<SearchConsoleApp.Services.Audit.AuditStaleRunWorker>();
builder.Services.AddHostedService<SearchConsoleApp.Services.MerchantCenter.ProductComplianceStaleRunWorker>();

// Outbox webhook HttpClient — ayrı timeout/retry policy
builder.Services.AddHttpClient("outbox-webhook", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("audit-crawl", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});

// === Pure API + Global Filters ===
builder.Services
    .AddControllers(o =>
    {
        o.Filters.Add<GlobalExceptionFilter>();
        o.Filters.Add<SearchConsoleApp.Web.Framework.Auditing.AuditFilter>();
    })
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o => o.SuppressMapClientErrors = false);

// === API Versioning ===
builder.Services.AddSearchConsoleAppVersioning();

// === JWT + Authorization ===
builder.Services.AddSearchConsoleAppJwtAuth(builder.Configuration);

// === PreAuth token store (2FA flow) - cache provider'a göre memory/redis ===
builder.Services.AddSearchConsoleAppPreAuthStore(builder.Configuration);

// === CORS ===
builder.Services.AddCors(o => o.AddPolicy("SearchConsoleAppCors", p =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? new[] { "http://localhost:4200", "http://localhost:4201", "http://localhost:4202" };
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

// === OpenAPI ===
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("public", new OpenApiInfo { Title = "SearchConsoleApp Public API", Version = "v1" });
    c.SwaggerDoc("web",    new OpenApiInfo { Title = "SearchConsoleApp Web API",    Version = "v1" });
    c.SwaggerDoc("admin",  new OpenApiInfo { Title = "SearchConsoleApp Admin API",  Version = "v1" });

    c.DocInclusionPredicate((doc, api) =>
    {
        var path = api.RelativePath ?? "";
        // Yeni route format: api/v{version}/{audience}/...
        if (doc == "public") return path.Contains("/public/") || path.StartsWith("api/v1/public");
        if (doc == "web")    return path.Contains("/web/") || path.StartsWith("api/v1/web");
        if (doc == "admin")  return path.Contains("/admin/") || path.StartsWith("api/v1/admin");
        return false;
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference
            { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/public/swagger.json", "Public");
        c.SwaggerEndpoint("/swagger/web/swagger.json", "Web");
        c.SwaggerEndpoint("/swagger/admin/swagger.json", "Admin");
    });
}

// === Middleware pipeline ===
app.UseHttpsRedirection();
app.UseSerilogRequestLogging();
app.UseCors("SearchConsoleAppCors");

// Local blob storage static serving (sadece local provider'da etkin)
app.UseSearchConsoleAppLocalBlobs(builder.Configuration);

// Localization (Accept-Language → request scope)
app.UseMiddleware<LocalizationMiddleware>();

// Maintenance mode — feature flag ile devre dışı bırakma (admin hariç 503)
app.UseMiddleware<SearchConsoleApp.Web.Framework.Middleware.MaintenanceMiddleware>();

// Rate limiter authentication'dan SONRA çalışsın (Customer-based partition'lar
// JWT'den okuyor)
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Audience tag enrichment
app.Use(async (ctx, next) =>
{
    var enricher = ctx.RequestServices.GetRequiredService<AudienceTagEnricher>();
    var scope = ctx.RequestServices.GetRequiredService<SearchConsoleApp.Core.RequestScope.IRequestScope>();
    await enricher.InvokeAsync(ctx, async _ => await next(), scope);
});

// === Health endpoints ===
// /health → tüm checks, /health/ready → "ready" tag'li, /health/live → liveness
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteJson
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteJson
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,  // sadece HTTP'nin yanıt vermesi yeterli
    ResponseWriter = HealthResponseWriter.WriteJson
});

app.MapControllers();
app.MapSearchConsoleAppRealtime(builder.Configuration);

// === Dev: migration + seed ===
if (app.Environment.IsDevelopment())
{
    await SearchConsoleApp.Web.Seeding.DbSeeder.SeedAsync(app.Services);
}

app.Run();

// Integration test projesinin WebApplicationFactory<Program> kullanabilmesi için
// Program sınıfı public olmalı. Top-level statements implicit internal Program
// üretiyor; aşağıdaki partial declaration onu public yapar.
public partial class Program { }
