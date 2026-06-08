namespace SearchConsoleApp.Services.Email;

/// <summary>
/// Basit inline email template'leri. Production'da Liquid/Razor template
/// engine (örn. Scriban, RazorLight) ile dışa alınabilir.
///
/// Bu sınıf tasarım kararı olarak basit: dependency-free, tek dosyada,
/// kolay özelleştirilebilir. Customer için marka/dil/tenant'a göre
/// branching gerekirse template engine'e geçilir.
/// </summary>
public static class EmailTemplates
{
    public static EmailMessage EmailVerification(string toEmail, string verificationUrl, string appName = "SearchConsoleApp")
    {
        var html = $$"""
            <!DOCTYPE html>
            <html><body style="font-family: -apple-system, sans-serif; max-width: 600px; margin: 0 auto;">
              <h2>{{appName}} — Email Doğrulama</h2>
              <p>Merhaba,</p>
              <p>Hesabınızı doğrulamak için aşağıdaki bağlantıya tıklayın:</p>
              <p style="text-align: center; margin: 32px 0;">
                <a href="{{verificationUrl}}"
                   style="background:#2563eb;color:white;padding:12px 24px;
                          text-decoration:none;border-radius:6px;display:inline-block;">
                  Email'i Doğrula
                </a>
              </p>
              <p style="color:#666;font-size:13px;">
                Bağlantı 24 saat geçerlidir. Eğer bu işlemi siz başlatmadıysanız,
                bu maili güvenle göz ardı edebilirsiniz.
              </p>
              <p style="color:#999;font-size:11px;margin-top:48px;">
                Bağlantı çalışmıyorsa: <br>{{verificationUrl}}
              </p>
            </body></html>
            """;

        var plain = $$"""
            {{appName}} — Email Doğrulama

            Hesabınızı doğrulamak için aşağıdaki bağlantıya tıklayın:
            {{verificationUrl}}

            Bağlantı 24 saat geçerlidir.
            """;

        return new EmailMessage(
            To: toEmail,
            Subject: $"{appName} — Email Doğrulama",
            HtmlBody: html,
            PlainTextBody: plain);
    }

    public static EmailMessage PasswordReset(string toEmail, string resetUrl, string appName = "SearchConsoleApp")
    {
        var html = $$"""
            <!DOCTYPE html>
            <html><body style="font-family: -apple-system, sans-serif; max-width: 600px; margin: 0 auto;">
              <h2>{{appName}} — Şifre Sıfırlama</h2>
              <p>Merhaba,</p>
              <p>Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın:</p>
              <p style="text-align: center; margin: 32px 0;">
                <a href="{{resetUrl}}"
                   style="background:#2563eb;color:white;padding:12px 24px;
                          text-decoration:none;border-radius:6px;display:inline-block;">
                  Şifreyi Sıfırla
                </a>
              </p>
              <p style="color:#666;font-size:13px;">
                Bağlantı 1 saat geçerlidir. Eğer bu isteği siz yapmadıysanız,
                hesabınız güvende — bu maili göz ardı edin.
              </p>
              <p style="color:#999;font-size:11px;margin-top:48px;">
                Bağlantı çalışmıyorsa: <br>{{resetUrl}}
              </p>
            </body></html>
            """;

        var plain = $$"""
            {{appName}} — Şifre Sıfırlama

            Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın:
            {{resetUrl}}

            Bağlantı 1 saat geçerlidir.
            """;

        return new EmailMessage(
            To: toEmail,
            Subject: $"{appName} — Şifre Sıfırlama",
            HtmlBody: html,
            PlainTextBody: plain);
    }
}
