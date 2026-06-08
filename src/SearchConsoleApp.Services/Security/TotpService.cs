using System.Security.Cryptography;
using System.Text;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Security;

/// <summary>
/// TOTP (RFC 6238) implementasyonu. Standart authenticator app'leri
/// (Google Authenticator, Authy, 1Password, Microsoft Authenticator) ile uyumlu.
///
/// Parametreler:
/// - Algorithm: HMAC-SHA1 (standart)
/// - Digits: 6
/// - Period: 30 saniye
/// - Clock skew: ±1 window (≈90 saniye tolerans)
///
/// Production'da OtpNet veya benzeri kütüphane tercih edilebilir, ama bu
/// implementasyon dependency-free ve test edilebilir.
/// </summary>
public interface ITotpService
{
    /// <summary>Yeni secret üret (Base32 encoded, 32 char).</summary>
    string GenerateSecret();

    /// <summary>Authenticator app QR'ı için otpauth:// URI.</summary>
    string BuildOtpAuthUri(string secret, string accountName, string issuer);

    /// <summary>Verilen kodu doğrula. Clock skew ±1 window içinde.</summary>
    bool VerifyCode(string secret, string code);
}

public class TotpService : ITotpService, ISingletonService
{
    private const int Digits = 6;
    private const int Period = 30;

    public string GenerateSecret()
    {
        // 20 byte = 160 bit (RFC önerisi)
        var bytes = RandomNumberGenerator.GetBytes(20);
        return Base32Encode(bytes);
    }

    public string BuildOtpAuthUri(string secret, string accountName, string issuer)
    {
        // Format: otpauth://totp/{issuer}:{account}?secret=...&issuer=...&algorithm=SHA1&digits=6&period=30
        Func<string, string> enc = Uri.EscapeDataString;
        return $"otpauth://totp/{enc(issuer)}:{enc(accountName)}" +
               $"?secret={secret}&issuer={enc(issuer)}&algorithm=SHA1&digits={Digits}&period={Period}";
    }

    public bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim().Replace(" ", "");
        if (code.Length != Digits) return false;

        byte[] key;
        try { key = Base32Decode(secret); }
        catch { return false; }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var counter = now / Period;

        // Clock skew ±1 window — ağ gecikmesi veya hafif saat farkı için
        for (long offset = -1; offset <= 1; offset++)
        {
            var expected = ComputeHotp(key, counter + offset);
            if (FixedTimeEquals(expected, code)) return true;
        }
        return false;
    }

    private static string ComputeHotp(byte[] key, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);
        var otp = binary % (int)Math.Pow(10, Digits);
        return otp.ToString(new string('0', Digits));
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    // === Base32 (RFC 4648) ===

    private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string Base32Encode(byte[] bytes)
    {
        var sb = new StringBuilder();
        int buffer = 0, bitsLeft = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Chars[(buffer >> bitsLeft) & 0x1F]);
            }
        }
        if (bitsLeft > 0)
        {
            sb.Append(Base32Chars[(buffer << (5 - bitsLeft)) & 0x1F]);
        }
        return sb.ToString();
    }

    private static byte[] Base32Decode(string s)
    {
        s = s.Trim().TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bitsLeft = 0;
        foreach (var c in s)
        {
            var idx = Base32Chars.IndexOf(c);
            if (idx < 0) throw new FormatException("Invalid Base32 character");
            buffer = (buffer << 5) | idx;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        return output.ToArray();
    }
}
