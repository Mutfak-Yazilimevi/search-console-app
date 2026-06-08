using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SearchConsoleApp.Services.Email;

/// <summary>
/// System.Net.Mail tabanlı SMTP gönderici.
///
/// Config: appsettings.json → Email:Smtp:{Host, Port, Username, Password, EnableSsl, FromEmail, FromName}
///
/// Production-grade alternatifler:
/// - SendGrid (sendgrid-csharp)
/// - Amazon SES (AWSSDK.SimpleEmail)
/// - Postmark (Postmark.AspNetCore)
/// - Mailgun (FluentEmail.Mailgun)
///
/// FluentEmail kütüphanesi tüm bu sağlayıcıları soyutlar — production'a
/// geçişte değerlendirilebilir.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var section = _config.GetSection("Email:Smtp");
        var host = section["Host"] ?? throw new InvalidOperationException("Email:Smtp:Host eksik.");
        var port = int.Parse(section["Port"] ?? "587");
        var username = section["Username"];
        var password = section["Password"];
        var enableSsl = bool.Parse(section["EnableSsl"] ?? "true");

        var fromEmail = message.FromEmail ?? section["FromEmail"]
            ?? throw new InvalidOperationException("Email:Smtp:FromEmail veya message.FromEmail gerekli.");
        var fromName = message.FromName ?? section["FromName"] ?? "SearchConsoleApp";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrEmpty(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(message.To);

        if (!string.IsNullOrEmpty(message.PlainTextBody))
        {
            var plainView = AlternateView.CreateAlternateViewFromString(
                message.PlainTextBody, null, "text/plain");
            mail.AlternateViews.Add(plainView);
        }

        await client.SendMailAsync(mail, ct);
        _logger.LogInformation("Email gönderildi: To={To}, Subject={Subject}", message.To, message.Subject);
    }
}

/// <summary>
/// Dev modu için: SMTP'ye bağlanmaz, email'i log'a yazar.
/// `appsettings.Development.json` → "Email": { "Mode": "log" } ile aktive edilir.
/// </summary>
public class LogEmailSender : IEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;
    public LogEmailSender(ILogger<LogEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "📧 [LOG-ONLY] To: {To} | Subject: {Subject}\n--- HTML ---\n{Html}\n--- /HTML ---",
            message.To, message.Subject, message.HtmlBody);
        return Task.CompletedTask;
    }
}
