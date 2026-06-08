using Microsoft.AspNetCore.Http;
using SearchConsoleApp.Services.Localization;

namespace SearchConsoleApp.Web.Framework.Localization;

/// <summary>
/// Accept-Language header'ı parse eder, request scope'taki localization
/// service'in CurrentLanguage'ini set eder.
///
/// q-value öncelik: "tr-TR,tr;q=0.9,en;q=0.8" → "tr"
/// </summary>
public class LocalizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string[] _supportedLanguages;

    public LocalizationMiddleware(RequestDelegate next, Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _next = next;
        _supportedLanguages = config.GetSection("App:SupportedLanguages").Get<string[]>()
            ?? new[] { "en", "tr" };
    }

    public async Task InvokeAsync(HttpContext context, ILocalizationService localizer)
    {
        // Öncelik sırası:
        // 1. ?lang=tr query parameter (tek seferlik override)
        // 2. Customer.Language claim (login halindeyse kalıcı tercih)
        // 3. Accept-Language header
        // 4. App:DefaultLanguage (service constructor'da set edildi)
        string? lang = null;

        if (context.Request.Query.TryGetValue("lang", out var queryLang))
        {
            lang = ResolveSupported(queryLang.ToString());
        }

        if (lang == null && context.User?.FindFirst("lang")?.Value is { Length: > 0 } claimLang)
        {
            lang = ResolveSupported(claimLang);
        }

        lang ??= ParseAcceptLanguage(context.Request.Headers.AcceptLanguage.ToString());

        if (lang != null && localizer is JsonLocalizationService impl)
        {
            impl.CurrentLanguage = lang;
        }

        await _next(context);
    }

    private string? ResolveSupported(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate)) return null;
        var lang = candidate.Split('-')[0].ToLowerInvariant();
        return _supportedLanguages.Contains(lang) ? lang : null;
    }

    private string? ParseAcceptLanguage(string header)
    {
        if (string.IsNullOrEmpty(header)) return null;

        // "tr-TR,tr;q=0.9,en;q=0.8" → ["tr-TR", "tr", "en"]
        var langs = header.Split(',')
            .Select(part => part.Split(';')[0].Trim())
            .Select(l => l.Split('-')[0].ToLowerInvariant())  // "tr-TR" → "tr"
            .Distinct();

        foreach (var lang in langs)
        {
            if (_supportedLanguages.Contains(lang)) return lang;
        }
        return null;
    }
}
