using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Domain.Theming;
using SearchConsoleApp.Data;
using SearchConsoleApp.Services.Security;

namespace SearchConsoleApp.Web.Seeding;

/// <summary>
/// İlk kurulum için seed data. Boş DB'de çalıştırılır, sonradan idempotent
/// (kayıt varsa yazmaz).
///
/// Kullanım: Program.cs'te `await DbSeeder.SeedAsync(app.Services);`
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SearchConsoleAppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SearchConsoleAppDbContext>>();

        await context.Database.MigrateAsync();

        await SeedDefaultThemesAsync(context, logger);
        await SeedDefaultAdminAsync(context, passwordHasher, logger);

        await context.SaveChangesAsync();
    }

    private static async Task SeedDefaultThemesAsync(SearchConsoleAppDbContext context, ILogger logger)
    {
        if (await context.Set<Theme>().AnyAsync()) return;

        var now = DateTime.UtcNow;
        context.Set<Theme>().AddRange(
            new Theme
            {
                EntityId = Guid.CreateVersion7(),
                Name = "default-light",
                DisplayName = "Default Light",
                Mode = "light",
                Active = true,
                JsonContent = DefaultLightJson(),
                CreatedOnUtc = now,
                UpdatedOnUtc = now,
            },
            new Theme
            {
                EntityId = Guid.CreateVersion7(),
                Name = "default-dark",
                DisplayName = "Default Dark",
                Mode = "dark",
                Active = true,
                JsonContent = DefaultDarkJson(),
                CreatedOnUtc = now,
                UpdatedOnUtc = now,
            }
        );

        logger.LogInformation("Seeded 2 default themes.");
    }

    private static async Task SeedDefaultAdminAsync(SearchConsoleAppDbContext context, IPasswordHasher hasher, ILogger logger)
    {
        var adminEmail = "admin@SearchConsoleApp.local";
        if (await context.Set<Customer>().AnyAsync(c => c.Email == adminEmail)) return;

        // ⚠️ Geliştirme için. Production'da bu seed CHANGE et veya sil.
        var defaultPassword = "Admin123!";

        context.Set<Customer>().Add(new Customer
        {
            EntityId = Guid.CreateVersion7(),
            Email = adminEmail,
            FirstName = "Default",
            LastName = "Admin",
            PasswordHash = hasher.Hash(defaultPassword),
            Active = true,
            EmailConfirmed = true,
            Roles = "user,admin",
            CreatedOnUtc = DateTime.UtcNow,
        });

        logger.LogWarning("Seeded default admin: {Email} / {Password} — PRODUCTION'DA DEĞİŞTİR!",
            adminEmail, defaultPassword);
    }

    private static string DefaultLightJson() => """
    {
      "name": "default-light",
      "displayName": "Default Light",
      "mode": "light",
      "colors": {
        "primary": "#2563eb",
        "primaryHover": "#1d4ed8",
        "primaryActive": "#1e40af",
        "primaryForeground": "#ffffff",
        "success": "#16a34a",
        "warning": "#ea580c",
        "danger": "#dc2626",
        "info": "#0891b2",
        "background": "#ffffff",
        "surface": "#f8fafc",
        "surfaceElevated": "#ffffff",
        "text": "#0f172a",
        "textMuted": "#475569",
        "textSubtle": "#94a3b8",
        "border": "#e2e8f0",
        "borderStrong": "#cbd5e1"
      },
      "radius": { "sm": "0.25rem", "md": "0.5rem", "lg": "0.75rem", "full": "9999px" }
    }
    """;

    private static string DefaultDarkJson() => """
    {
      "name": "default-dark",
      "displayName": "Default Dark",
      "mode": "dark",
      "colors": {
        "primary": "#3b82f6",
        "primaryHover": "#60a5fa",
        "primaryActive": "#2563eb",
        "primaryForeground": "#ffffff",
        "success": "#22c55e",
        "warning": "#f97316",
        "danger": "#ef4444",
        "info": "#06b6d4",
        "background": "#0a0a0a",
        "surface": "#171717",
        "surfaceElevated": "#262626",
        "text": "#fafafa",
        "textMuted": "#a3a3a3",
        "textSubtle": "#737373",
        "border": "#262626",
        "borderStrong": "#404040"
      },
      "radius": { "sm": "0.25rem", "md": "0.5rem", "lg": "0.75rem", "full": "9999px" }
    }
    """;
}
