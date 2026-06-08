using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Services.Theming;
using SearchConsoleApp.Web.Framework.Api;

namespace SearchConsoleApp.Web.Controllers.Public;

/// <summary>
/// Public tema endpoint'leri. Frontend (Angular + RN) boot'ta bu endpoint'ten
/// tema yükler. Multi-tenant senaryosunda her tenant'ın subdomain/slug'ına
/// göre kendi temaları döner (docs/MULTI_TENANCY.md → "Tenant'a özel tema").
///
/// Route: /api/public/themes/*
/// Cache-Control: public, max-age=300 — CDN'de 5 dakika cache'lenebilir.
/// </summary>
public class ThemesController : PublicApiController
{
    private readonly IThemeService _themeService;
    public ThemesController(IThemeService themeService) => _themeService = themeService;

    /// <summary>
    /// Aktif tüm temaların metadata listesi. Frontend ThemeSwitcher
    /// dropdown'ı bunu kullanır.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var themes = await _themeService.GetActiveThemesAsync();
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(themes.Select(t => new
        {
            t.Name,
            t.DisplayName,
            t.Mode,
        }));
    }

    /// <summary>
    /// Tek tema — tam JSON içeriği. Frontend ThemeLoaderService bunu çağırır.
    /// </summary>
    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name)
    {
        var theme = await _themeService.GetThemeByNameAsync(name);
        if (theme == null) return NotFoundResult($"Theme '{name}' not found");

        // JsonContent string olarak DB'de, parse edip kullanıcıya nesne döner
        Response.Headers.CacheControl = "public, max-age=300";
        try
        {
            using var doc = JsonDocument.Parse(theme.JsonContent);
            return Ok(doc.RootElement.Clone());
        }
        catch (JsonException)
        {
            return Problem(statusCode: 500, title: "Theme content is corrupted");
        }
    }
}
