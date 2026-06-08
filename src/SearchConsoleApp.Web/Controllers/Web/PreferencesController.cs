using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core.RequestScope;
using SearchConsoleApp.Services.Customers;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;

namespace SearchConsoleApp.Web.Controllers.Web;

public record UpdateLanguageRequest(string Language);

/// <summary>
/// Kullanıcı tercihleri.
/// Route: /api/v1/web/preferences/*
/// </summary>
public class PreferencesController : WebApiController
{
    private readonly ICustomerService _customers;
    private readonly IRequestScope _scope;
    private readonly string[] _supportedLanguages;

    public PreferencesController(
        ICustomerService customers,
        IRequestScope scope,
        IConfiguration config)
    {
        _customers = customers;
        _scope = scope;
        _supportedLanguages = config.GetSection("App:SupportedLanguages").Get<string[]>()
            ?? new[] { "en", "tr" };
    }

    [HttpGet("language")]
    public async Task<IActionResult> GetLanguage()
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();
        var customer = await _customers.GetCustomerByIdAsync(customerId);
        return Ok(new { language = customer?.Language });
    }

    /// <summary>
    /// Dil tercihini güncelle. JWT'deki 'lang' claim'i sonraki login'de güncellenir
    /// (mevcut token'da değişmez — kullanıcı tekrar login olunca devreye girer).
    /// </summary>
    [HttpPut("language")]
    [Audit("preferences.language_change")]
    public async Task<IActionResult> UpdateLanguage([FromBody] UpdateLanguageRequest req)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var lang = req.Language?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(lang) || !_supportedLanguages.Contains(lang))
            return Problem(statusCode: 400, title: $"Desteklenmeyen dil. Geçerli: {string.Join(", ", _supportedLanguages)}");

        var customer = await _customers.GetCustomerByIdAsync(customerId);
        if (customer == null) return Unauthorized();

        customer.Language = lang;
        await _customers.UpdateCustomerAsync(customer);

        return Ok(new { language = lang });
    }
}
