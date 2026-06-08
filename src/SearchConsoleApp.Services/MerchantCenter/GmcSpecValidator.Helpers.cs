using SearchConsoleApp.Core.Domain.MerchantCenter;

namespace SearchConsoleApp.Services.MerchantCenter;

public partial class GmcSpecValidator
{
    private static string FormatPriceMismatchEvidence(
        decimal? schemaPrice,
        decimal? visiblePrice,
        string? currency,
        decimal? schemaListPrice = null)
    {
        var cur = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim().ToUpperInvariant();
        var lines = new List<string>
        {
            $"Schema (JSON-LD Offer.price): {FormatMoney(schemaPrice, cur)}",
            $"Sayfada görünen fiyat: {FormatMoney(visiblePrice, cur)}",
        };
        if (schemaListPrice != null)
            lines.Add($"Schema liste fiyatı: {FormatMoney(schemaListPrice, cur)}");
        return string.Join('\n', lines);
    }

    private static string FormatMoney(decimal? amount, string currency)
        => amount == null ? "—" : $"{amount.Value:0.##} {currency}";

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url.Trim().TrimEnd('/');
        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}".TrimEnd('/').ToLowerInvariant();
    }

    private static ProductComplianceIssueSeverity ParseSeverity(string severity)
        => severity.ToLowerInvariant() switch
        {
            "critical" => ProductComplianceIssueSeverity.Critical,
            "warning" => ProductComplianceIssueSeverity.Warning,
            _ => ProductComplianceIssueSeverity.Info,
        };

    private static bool TitleHasPromoSpam(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(
            title,
            @"(ücretsiz\s*kargo|%\s*\d+\s*indirim|en\s*ucuz|taksit\s*0|hemen\s*al|fırsat|kampanya|!!!)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static bool TitleMostlyCaps(string? title)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length < 8) return false;
        var letters = System.Text.RegularExpressions.Regex.Replace(title, @"[^A-Za-zÇĞİÖŞÜçğıöşü]", "");
        if (letters.Length < 6) return false;
        var upper = System.Text.RegularExpressions.Regex.Replace(letters, @"[^A-ZÇĞİÖŞÜ]", "").Length;
        return upper / (double)letters.Length > 0.6;
    }
}
