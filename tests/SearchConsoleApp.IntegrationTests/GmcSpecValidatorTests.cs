using FluentAssertions;
using SearchConsoleApp.Services.MerchantCenter;
using Xunit;

namespace SearchConsoleApp.IntegrationTests;

public class GmcSpecValidatorTests
{
    private readonly GmcSpecValidator _validator = new(new GmcRulesLoader());

    [Fact]
    public void ValidateProduct_flags_promotional_image_url()
    {
        var data = MinimalProduct();
        data.ImageLooksPromotional = true;
        data.Image = "https://cdn.example.com/promo-banner-shoe.jpg";

        var issues = _validator.ValidateProduct(data, data.Url);

        issues.Should().Contain(i => i.RuleId == "GMC-SPEC-031");
    }

    [Fact]
    public void ValidateProduct_flags_missing_identifiers_and_schema()
    {
        var data = new ExtractedProductData
        {
            Url = "https://shop.example.com/urun/test",
            Name = "Test Ürün",
            IsHttps = true,
            HasProductSchema = false,
        };

        var issues = _validator.ValidateProduct(data, data.Url);

        issues.Should().Contain(i => i.RuleId == "GMC-SPEC-001");
        issues.Should().Contain(i => i.RuleId == "GMC-SPEC-011");
    }

    [Fact]
    public void ValidateProduct_flags_title_promo_spam()
    {
        var data = MinimalProduct();
        data.Name = "ÜCRETSİZ KARGO %50 İNDİRİM Ayakkabı";

        var issues = _validator.ValidateProduct(data, data.Url);

        issues.Should().Contain(i => i.RuleId == "GMC-SPEC-019");
    }

    [Fact]
    public void ValidateCrossProducts_flags_duplicate_title_with_urls_in_evidence()
    {
        var p1 = MinimalProduct("https://shop.example.com/urun/kirmizi", gtin: "8690000000001");
        p1.Name = "Aynı Başlık";
        p1.Sku = "SKU-KIRMIZI";
        p1.Color = "Kırmızı";
        var p2 = MinimalProduct("https://shop.example.com/urun/mavi", gtin: "8690000000002");
        p2.Name = "Aynı Başlık";
        p2.Sku = "SKU-MAVI";
        p2.Color = "Mavi";

        var products = new List<(long ItemId, string PageUrl, ExtractedProductData Data)>
        {
            (1, "https://shop.example.com/urun/kirmizi", p1),
            (2, "https://shop.example.com/urun/mavi", p2),
        };

        var issues = _validator.ValidateCrossProducts(products);

        var issue = issues.Should().ContainSingle(i => i.RuleId == "GMC-X-001").Subject;
        issue.Evidence.Should().Contain("Başlık: \"Aynı Başlık\"");
        issue.Evidence.Should().Contain("https://shop.example.com/urun/kirmizi");
        issue.Evidence.Should().Contain("SKU: SKU-KIRMIZI");
        issue.Message.Should().Contain("Aynı Başlık");
    }

    [Fact]
    public void ValidateCrossProducts_uses_page_url_when_extracted_url_missing()
    {
        var p1 = MinimalProduct("https://shop.example.com/urun/a");
        p1.Url = "";
        p1.Name = "Ortak Title";
        var p2 = MinimalProduct("https://shop.example.com/urun/b");
        p2.Url = "";
        p2.Name = "Ortak Title";

        var products = new List<(long ItemId, string PageUrl, ExtractedProductData Data)>
        {
            (1, "https://shop.example.com/urun/a", p1),
            (2, "https://shop.example.com/urun/b", p2),
        };

        var issue = _validator.ValidateCrossProducts(products).Should().ContainSingle(i => i.RuleId == "GMC-X-001").Subject;
        issue.Evidence.Should().Contain("https://shop.example.com/urun/a");
        issue.Evidence.Should().Contain("https://shop.example.com/urun/b");
    }

    [Fact]
    public void ValidateCrossProducts_flags_duplicate_gtin()
    {
        var products = new List<(long ItemId, string PageUrl, ExtractedProductData Data)>
        {
            (1, "https://shop.example.com/a", MinimalProduct("https://shop.example.com/a", "8690000000001")),
            (2, "https://shop.example.com/b", MinimalProduct("https://shop.example.com/b", "8690000000001")),
        };

        var issues = _validator.ValidateCrossProducts(products);

        issues.Should().Contain(i => i.RuleId == "GMC-X-002");
    }

    [Fact]
    public void ValidateCrossProducts_flags_duplicate_description_with_urls_in_evidence()
    {
        var sharedDescription = new string('x', 60);
        var p1 = MinimalProduct("https://shop.example.com/urun/1");
        p1.Description = sharedDescription;
        p1.Color = "Kırmızı";
        var p2 = MinimalProduct("https://shop.example.com/urun/2");
        p2.Description = sharedDescription;
        p2.Color = "Mavi";
        var p3 = MinimalProduct("https://shop.example.com/urun/3");
        p3.Description = sharedDescription;

        var products = new List<(long ItemId, string PageUrl, ExtractedProductData Data)>
        {
            (1, "https://shop.example.com/urun/1", p1),
            (2, "https://shop.example.com/urun/2", p2),
            (3, "https://shop.example.com/urun/3", p3),
        };

        var issues = _validator.ValidateCrossProducts(products);

        var issue = issues.Should().ContainSingle(i => i.RuleId == "GMC-X-004").Subject;
        issue.Evidence.Should().Contain("Açıklama:");
        issue.Evidence.Should().Contain(" — https://shop.example.com/urun/1");
        issue.Evidence.Should().Contain("Renk: Kırmızı");
        issue.Message.Should().Contain("Aynı açıklama");
    }

    [Fact]
    public void ValidateProduct_flags_schema_visible_price_mismatch_with_readable_evidence()
    {
        var data = MinimalProduct();
        data.SchemaPrice = 199.99m;
        data.VisiblePrice = 149.99m;
        data.PriceCurrency = "TRY";

        var issue = _validator.ValidateProduct(data, data.Url)
            .Should().ContainSingle(i => i.RuleId == "GMC-SPEC-012").Subject;

        issue.Evidence.Should().Contain("Schema (JSON-LD Offer.price): 199,99 TRY");
        issue.Evidence.Should().Contain("Sayfada görünen fiyat: 149,99 TRY");
    }

    private static ExtractedProductData MinimalProduct(
        string url = "https://shop.example.com/urun/ayakkabi",
        string? gtin = null) => new()
    {
        Url = url,
        Name = "Marka Ayakkabı 42",
        Sku = "SKU-42",
        Gtin = gtin,
        Brand = "Marka",
        Image = "https://cdn.example.com/shoe.jpg",
        Images = ["https://cdn.example.com/shoe.jpg", "https://cdn.example.com/shoe2.jpg"],
        ImageCount = 2,
        SchemaPrice = 999,
        VisiblePrice = 999,
        PriceCurrency = "TRY",
        Availability = "InStock",
        Condition = "new",
        HasProductSchema = true,
        IsHttps = true,
        HasViewportMeta = true,
        GoogleProductCategory = "Apparel & Accessories > Shoes",
        HasShippingDetails = true,
    };
}
