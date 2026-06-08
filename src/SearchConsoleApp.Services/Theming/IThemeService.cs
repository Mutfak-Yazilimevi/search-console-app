using SearchConsoleApp.Core.Domain.Theming;

namespace SearchConsoleApp.Services.Theming;

public interface IThemeService
{
    /// <summary>Aktif (Active=true) tüm temaları listeler — public endpoint için.</summary>
    Task<IList<Theme>> GetActiveThemesAsync();

    /// <summary>İsimle tema getir. Inactive temalar görünmez.</summary>
    Task<Theme?> GetThemeByNameAsync(string name);

    /// <summary>Tema ekle/güncelle — admin endpoint'ler bunu çağırır.</summary>
    Task UpsertAsync(Theme theme);

    /// <summary>Tema sil (soft).</summary>
    Task DeleteAsync(Theme theme);
}
