using System.Security.Cryptography;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Security;

public interface IPasswordHasher
{
    /// <summary>Plaintext'i hashler — DB'ye yazılacak format.</summary>
    string Hash(string password);

    /// <summary>Plaintext, hash ile eşleşiyor mu?</summary>
    bool Verify(string password, string hash);
}

/// <summary>
/// PBKDF2-SHA256, 100k iterations, 16 byte salt, 32 byte hash.
/// Format: {iterations}.{base64salt}.{base64hash}
///
/// Production'da Argon2 daha güçlü ama .NET'te BCrypt.Net veya
/// Konscious.Security.Cryptography paketi gerekir. PBKDF2 .NET'in
/// kendi kütüphanesinde, dış bağımlılık yok.
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher, ISingletonService
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash)) return false;

        var parts = hash.Split('.');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var iterations)) return false;

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }
}
