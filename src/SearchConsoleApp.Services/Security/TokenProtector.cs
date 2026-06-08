using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Security;

public interface ITokenProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}

/// <summary>
/// AES encryption for OAuth refresh tokens at rest.
/// Uses a config key — replace in production with a secrets manager.
/// </summary>
public partial class ConfigTokenProtector : ITokenProtector, ISingletonService
{
    private readonly byte[] _key;

    public ConfigTokenProtector(IConfiguration config)
    {
        var secret = config["Security:TokenEncryptionKey"]
            ?? config["Jwt:Key"]
            ?? throw new InvalidOperationException("Security:TokenEncryptionKey or Jwt:Key required.");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string Protect(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        var combined = aes.IV.Concat(cipher).ToArray();
        return Convert.ToBase64String(combined);
    }

    public string Unprotect(string protectedText)
    {
        var combined = Convert.FromBase64String(protectedText);
        using var aes = Aes.Create();
        aes.Key = _key;
        var iv = combined.Take(16).ToArray();
        var cipher = combined.Skip(16).ToArray();
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(plain);
    }
}
