namespace SearchConsoleApp.Services.Security;

/// <summary>
/// 2FA setup ve doğrulama akışı:
///
/// 1. Setup başlat: GenerateSecretAsync → kullanıcıya QR + secret göster
/// 2. Setup tamamla: EnableAsync(code) → ilk geçerli code 2FA'yı aktif eder + recovery codes döner
/// 3. Login: VerifyAsync(code) → her giriş veya hassas işlemde
/// 4. Recovery: VerifyRecoveryCodeAsync(code) → kullanılan code geçersizleşir
/// 5. Disable: DisableAsync(password) → şifre doğrulamasıyla
///
/// Device.Trusted entegrasyonu: AuthService login'de Device.Trusted=true ise
/// 2FA atlanabilir (kullanıcı tercihine bağlı, default false). Bu sayede güvenli
/// cihazlardan her seferinde 2FA istenmez.
/// </summary>
public interface ITwoFactorService
{
    /// <summary>Setup için secret ve QR URI üret. DB'ye HENÜZ yazılmaz.</summary>
    Task<TwoFactorSetupInfo> StartSetupAsync(long customerId);

    /// <summary>Setup'ı tamamla. İlk geçerli code 2FA'yı aktive eder.</summary>
    /// <returns>Bir kez gösterilecek recovery code'ları (10 adet).</returns>
    Task<IReadOnlyList<string>> EnableAsync(long customerId, string secret, string code);

    /// <summary>Login akışında: kullanıcının verdiği code'u doğrula.</summary>
    Task<bool> VerifyAsync(long customerId, string code);

    /// <summary>Recovery code ile giriş. Kullanılan code geçersizleşir.</summary>
    Task<bool> VerifyRecoveryCodeAsync(long customerId, string code);

    /// <summary>2FA'yı kapat. Parola doğrulaması üst katmanda yapılmalı.</summary>
    Task DisableAsync(long customerId);

    /// <summary>Recovery code'ları yeniden üret (eski hepsi geçersizleşir).</summary>
    Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(long customerId);
}

public record TwoFactorSetupInfo(string Secret, string OtpAuthUri);
