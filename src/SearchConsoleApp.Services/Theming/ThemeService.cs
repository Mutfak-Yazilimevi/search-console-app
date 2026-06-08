using Microsoft.EntityFrameworkCore;
using SearchConsoleApp.Core.Caching;
using SearchConsoleApp.Core.Domain.Theming;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Theming;

public partial class ThemeService : IThemeService, IScopedService
{
    #region Cache Keys
    public static readonly CacheKey ActiveThemesKey = new(
        "SearchConsoleApp.theme.active.all",
        TimeSpan.FromMinutes(30),
        "SearchConsoleApp.theme.");
    public static readonly CacheKey ThemeByNameKey = new(
        "SearchConsoleApp.theme.byname.{0}",
        TimeSpan.FromMinutes(30),
        "SearchConsoleApp.theme.");
    public const string ThemePrefix = "SearchConsoleApp.theme.";
    #endregion

    private readonly IRepository<Theme> _themeRepository;
    private readonly IStaticCacheManager _cacheManager;

    public ThemeService(IRepository<Theme> themeRepository, IStaticCacheManager cacheManager)
    {
        _themeRepository = themeRepository;
        _cacheManager = cacheManager;
    }

    public virtual async Task<IList<Theme>> GetActiveThemesAsync()
    {
        return await _cacheManager.GetAsync(ActiveThemesKey, async () =>
            await _themeRepository.Table
                .Where(t => t.Active)
                .OrderBy(t => t.Name)
                .ToListAsync()
        ) ?? new List<Theme>();
    }

    public virtual async Task<Theme?> GetThemeByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = ThemeByNameKey.Create(name.ToLowerInvariant());
        return await _cacheManager.GetAsync(key, async () =>
            await _themeRepository.Table
                .FirstOrDefaultAsync(t => t.Name == name && t.Active));
    }

    public virtual async Task UpsertAsync(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        theme.UpdatedOnUtc = DateTime.UtcNow;
        if (theme.Id == 0)
        {
            theme.CreatedOnUtc = DateTime.UtcNow;
            await _themeRepository.InsertAsync(theme);
        }
        else
        {
            await _themeRepository.UpdateAsync(theme);
        }
        await _cacheManager.RemoveByPrefixAsync(ThemePrefix);
    }

    public virtual async Task DeleteAsync(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        await _themeRepository.DeleteAsync(theme);
        await _cacheManager.RemoveByPrefixAsync(ThemePrefix);
    }
}
