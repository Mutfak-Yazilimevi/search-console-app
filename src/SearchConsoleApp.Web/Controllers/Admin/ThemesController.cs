using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Core.Domain.Theming;
using SearchConsoleApp.Services.Theming;
using SearchConsoleApp.Web.Framework.Api;

namespace SearchConsoleApp.Web.Controllers.Admin;

public record ThemeDto(
    Guid EntityId,
    string Name,
    string DisplayName,
    string Mode,
    bool Active,
    object Content,
    DateTime CreatedOnUtc,
    DateTime UpdatedOnUtc);

public record ThemeUpsertRequest(
    string Name,
    string DisplayName,
    string Mode,
    bool Active,
    JsonElement Content);

/// <summary>
/// Admin: tema CRUD. Sadece admin rolü.
/// Route: /api/admin/themes/*
///
/// Müşteri başına özel tema oluşturmak için admin UI bu endpoint'leri kullanır.
/// Public endpoint (`/api/public/themes`) sadece Active=true olanları gösterir.
/// </summary>
public class ThemesController : AdminApiController
{
    private readonly IThemeService _themes;

    public ThemesController(IThemeService themes) => _themes = themes;

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var items = await _themes.GetActiveThemesAsync();
        return Ok(items.Select(ToDto));
    }

    [HttpGet("{entityId:guid}")]
    public async Task<IActionResult> Get(Guid entityId)
    {
        // ThemeService'te EntityId lookup yok — şimdilik Name üzerinden
        var items = await _themes.GetActiveThemesAsync();
        var theme = items.FirstOrDefault(t => t.EntityId == entityId);
        if (theme == null) return NotFoundResult();
        return Ok(ToDto(theme));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ThemeUpsertRequest req)
    {
        var theme = new Theme
        {
            Name = req.Name.Trim().ToLowerInvariant(),
            DisplayName = req.DisplayName,
            Mode = req.Mode,
            Active = req.Active,
            JsonContent = req.Content.GetRawText(),
        };
        await _themes.UpsertAsync(theme);
        return Ok(ToDto(theme));
    }

    [HttpPut("{entityId:guid}")]
    public async Task<IActionResult> Update(Guid entityId, [FromBody] ThemeUpsertRequest req)
    {
        var items = await _themes.GetActiveThemesAsync();
        var theme = items.FirstOrDefault(t => t.EntityId == entityId);
        if (theme == null) return NotFoundResult();

        theme.DisplayName = req.DisplayName;
        theme.Mode = req.Mode;
        theme.Active = req.Active;
        theme.JsonContent = req.Content.GetRawText();
        await _themes.UpsertAsync(theme);
        return Ok(ToDto(theme));
    }

    [HttpDelete("{entityId:guid}")]
    public async Task<IActionResult> Delete(Guid entityId)
    {
        var items = await _themes.GetActiveThemesAsync();
        var theme = items.FirstOrDefault(t => t.EntityId == entityId);
        if (theme == null) return NotFoundResult();

        await _themes.DeleteAsync(theme);
        return NoContent();
    }

    private static ThemeDto ToDto(Theme t)
    {
        object content;
        try
        {
            content = JsonDocument.Parse(t.JsonContent).RootElement.Clone();
        }
        catch
        {
            content = new { };
        }

        return new ThemeDto(
            t.EntityId, t.Name, t.DisplayName, t.Mode, t.Active,
            content, t.CreatedOnUtc, t.UpdatedOnUtc);
    }
}
