namespace SearchConsoleApp.Services.Email;

/// <summary>
/// Email gönderme soyutlaması.
///
/// Default impl: `SmtpEmailSender` (System.Net.Mail).
/// Production'da SendGrid, SES, Mailgun, Postmark gibi sağlayıcılarla
/// değiştirilir — interface aynı, sadece DI registration değişir.
///
/// Dev modda: `LogEmailSender` impl email'i log'a yazar, gerçek SMTP'ye
/// bağlanmaz. `Email:Mode = "log"` ile aktive olur.
///
/// Email gönderimi **async ve fail-tolerant** olmalı — register/reset
/// akışı email göndermesi başarısız olsa bile user-facing operasyon
/// başarılı sayılmalı (queue/retry pattern).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null,
    string? FromEmail = null,
    string? FromName = null);
