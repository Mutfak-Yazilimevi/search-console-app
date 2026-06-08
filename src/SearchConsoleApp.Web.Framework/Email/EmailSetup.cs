using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Services.Email;

namespace SearchConsoleApp.Web.Framework.Email;

public static class EmailSetup
{
    /// <summary>
    /// Email sender'ı config'e göre kaydeder.
    ///
    /// appsettings.json:
    ///   "Email": { "Mode": "smtp" | "log" }
    ///
    /// smtp → SmtpEmailSender (production)
    /// log  → LogEmailSender (dev, console'a yazar)
    ///
    /// Bu seçim marker interface ile yapılamıyor çünkü iki impl de
    /// IEmailSender'ı implement ediyor. Dinamik seçim için manuel kayıt.
    /// </summary>
    public static IServiceCollection AddSearchConsoleAppEmail(this IServiceCollection services, IConfiguration config)
    {
        var mode = config["Email:Mode"]?.ToLowerInvariant() ?? "smtp";

        if (mode == "log")
        {
            services.AddSingleton<IEmailSender, LogEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }

        return services;
    }
}
