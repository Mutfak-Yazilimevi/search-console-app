using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core.Domain.Customers;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;

namespace SearchConsoleApp.Services.Security;

public partial class TwoFactorService : ITwoFactorService, IScopedService
{
    private const int RecoveryCodeCount = 10;
    private const int RecoveryCodeLength = 10; // 10 char = ~50 bit entropy, kabul edilebilir

    private readonly IRepository<Customer> _customerRepo;
    private readonly ITotpService _totp;
    private readonly string _issuer;

    public TwoFactorService(
        IRepository<Customer> customerRepo,
        ITotpService totp,
        IConfiguration config)
    {
        _customerRepo = customerRepo;
        _totp = totp;
        _issuer = config["TwoFactor:Issuer"] ?? "SearchConsoleApp";
    }

    public virtual async Task<TwoFactorSetupInfo> StartSetupAsync(long customerId)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException("Customer bulunamadı.");

        var secret = _totp.GenerateSecret();
        var uri = _totp.BuildOtpAuthUri(secret, customer.Email, _issuer);

        // NOT: secret DB'ye burada yazılmaz — Enable çağrılana kadar tutulmaz.
        // Frontend secret'ı geçici saklar, EnableAsync'e tekrar gönderir.
        return new TwoFactorSetupInfo(secret, uri);
    }

    public virtual async Task<IReadOnlyList<string>> EnableAsync(long customerId, string secret, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (!_totp.VerifyCode(secret, code))
            throw new UnauthorizedAccessException("2FA kodu geçersiz.");

        var customer = await _customerRepo.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException("Customer bulunamadı.");

        var recoveryCodes = GenerateRecoveryCodes(RecoveryCodeCount);
        var hashedCodes = recoveryCodes.Select(HashRecoveryCode).ToList();

        customer.TotpSecret = secret;
        customer.TwoFactorEnabled = true;
        customer.RecoveryCodesHashes = string.Join(",", hashedCodes);
        await _customerRepo.UpdateAsync(customer);

        return recoveryCodes;
    }

    public virtual async Task<bool> VerifyAsync(long customerId, string code)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId);
        if (customer == null || !customer.TwoFactorEnabled || string.IsNullOrEmpty(customer.TotpSecret))
            return false;

        return _totp.VerifyCode(customer.TotpSecret, code);
    }

    public virtual async Task<bool> VerifyRecoveryCodeAsync(long customerId, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var normalized = code.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");

        var customer = await _customerRepo.GetByIdAsync(customerId);
        if (customer?.RecoveryCodesHashes == null) return false;

        var hashes = customer.RecoveryCodesHashes
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        var inputHash = HashRecoveryCode(normalized);

        var idx = hashes.IndexOf(inputHash);
        if (idx < 0) return false;

        // Kullanılan code'u kaldır (one-time use)
        hashes.RemoveAt(idx);
        customer.RecoveryCodesHashes = hashes.Count > 0 ? string.Join(",", hashes) : null;
        await _customerRepo.UpdateAsync(customer);

        return true;
    }

    public virtual async Task DisableAsync(long customerId)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId);
        if (customer == null) return;

        customer.TwoFactorEnabled = false;
        customer.TotpSecret = null;
        customer.RecoveryCodesHashes = null;
        await _customerRepo.UpdateAsync(customer);
    }

    public virtual async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(long customerId)
    {
        var customer = await _customerRepo.GetByIdAsync(customerId)
            ?? throw new InvalidOperationException("Customer bulunamadı.");
        if (!customer.TwoFactorEnabled)
            throw new InvalidOperationException("2FA aktif değil.");

        var newCodes = GenerateRecoveryCodes(RecoveryCodeCount);
        customer.RecoveryCodesHashes = string.Join(",", newCodes.Select(HashRecoveryCode));
        await _customerRepo.UpdateAsync(customer);

        return newCodes;
    }

    // === Helpers ===

    private static List<string> GenerateRecoveryCodes(int count)
    {
        var codes = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            // Format: XXXXX-XXXXX (5+5 char, dash ile insan-okur)
            var bytes = RandomNumberGenerator.GetBytes(8);
            var raw = Convert.ToBase64String(bytes)
                .Replace("/", "").Replace("+", "").Replace("=", "")
                .ToUpperInvariant();
            var code = raw.Substring(0, Math.Min(10, raw.Length));
            // Dash ile böl
            if (code.Length >= 10)
                code = code[..5] + "-" + code[5..10];
            codes.Add(code);
        }
        return codes;
    }

    private static string HashRecoveryCode(string code)
    {
        // Recovery code'ları SHA-256 ile hash — DB'ye plain kaydedilmez
        var normalized = code.ToUpperInvariant().Replace("-", "");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }
}
