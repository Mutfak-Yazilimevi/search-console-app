namespace SearchConsoleApp.Services.MerchantCenter;

/// <summary>
/// Yaygın Google Merchant Center issue kodları için Türkçe düzeltme önerileri.
/// </summary>
public static class GmcIssueFixHintMapper
{
    private static readonly Dictionary<string, string> Hints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["missing_gtin"] = "Ürün için geçerli GTIN (barkod) ekleyin veya identifier_exists:false ile MPN+brand kullanın.",
        ["invalid_gtin"] = "GTIN formatını kontrol edin; yanlış barkod reddedilir.",
        ["missing_price"] = "Offer.price ve priceCurrency (TRY) alanlarını feed ve schema'ya ekleyin.",
        ["invalid_price"] = "Fiyat pozitif sayı olmalı; para birimi TRY ile eşleşmeli.",
        ["price_mismatch"] = "Sayfa, schema ve feed fiyatlarını eşitleyin.",
        ["missing_image"] = "HTTPS erişilebilir ana ürün görseli ekleyin (min 100×100 px).",
        ["invalid_image"] = "Görsel URL'si erişilebilir ve HTTPS olmalı.",
        ["missing_availability"] = "availability değerini InStock, OutOfStock veya PreOrder olarak belirtin.",
        ["missing_brand"] = "Marka adını feed ve Product schema'ya ekleyin.",
        ["missing_condition"] = "condition alanına new, refurbished veya used yazın.",
        ["title_too_long"] = "Başlığı 150 karakter altına indirin.",
        ["missing_title"] = "Ürün başlığı zorunludur.",
        ["missing_description"] = "Benzersiz ve bilgilendirici ürün açıklaması ekleyin.",
        ["landing_page_error"] = "Ürün URL'si erişilebilir, HTTPS ve indexlenebilir olmalı.",
        ["policy_violation"] = "Google Merchant Center politika ihlali — Yardım merkezindeki ilgili maddeyi inceleyin.",
    };

    public static string Map(string? issueCode, string? description, string fallback)
    {
        if (string.IsNullOrWhiteSpace(issueCode))
            return InferFromDescription(description, fallback);

        if (Hints.TryGetValue(issueCode, out var hint))
            return hint;

        var normalized = issueCode.Replace('-', '_').ToLowerInvariant();
        if (Hints.TryGetValue(normalized, out hint))
            return hint;

        foreach (var (key, value) in Hints)
        {
            if (normalized.Contains(key, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return InferFromDescription(description, fallback);
    }

    private static string InferFromDescription(string? description, string fallback)
    {
        if (string.IsNullOrWhiteSpace(description)) return fallback;
        var lower = description.ToLowerInvariant();
        if (lower.Contains("gtin") || lower.Contains("identifier"))
            return Hints["missing_gtin"];
        if (lower.Contains("price") || lower.Contains("fiyat"))
            return Hints["missing_price"];
        if (lower.Contains("image") || lower.Contains("görsel"))
            return Hints["missing_image"];
        if (lower.Contains("availability") || lower.Contains("stok"))
            return Hints["missing_availability"];
        return fallback;
    }
}
