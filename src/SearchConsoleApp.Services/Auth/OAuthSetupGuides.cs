using Microsoft.Extensions.Configuration;

namespace SearchConsoleApp.Services.Auth;

public static class OAuthSetupGuides
{
    public static OAuthSetupGuide ForGoogleLogin(IConfiguration config)
    {
        var redirectUri = config["OAuth:google:RedirectUri"]
            ?? "http://localhost:4200/auth/external/google/callback";
        var clientId = config["OAuth:google:ClientId"];
        var clientSecret = config["OAuth:google:ClientSecret"];

        return new OAuthSetupGuide
        {
            Provider = "google",
            Purpose = "login",
            Title = "Google OAuth yapılandırması eksik",
            Summary = "Google ile giriş için OAuth Client ID tanımlanmamış. Aşağıdaki adımları izleyerek Google Cloud Console'da kimlik bilgisi oluşturup sunucuya ekleyin.",
            RedirectUri = redirectUri,
            EnvFileHint = "Proje kökünde .env.example dosyasını .env olarak kopyalayın; değerleri doldurduktan sonra API'yi yeniden başlatın (docker compose up -d --build api).",
            Links =
            [
                new OAuthSetupLink
                {
                    Label = "Google Cloud Console — Credentials",
                    Url = "https://console.cloud.google.com/apis/credentials",
                },
                new OAuthSetupLink
                {
                    Label = "OAuth consent screen",
                    Url = "https://console.cloud.google.com/apis/credentials/consent",
                },
                new OAuthSetupLink
                {
                    Label = "Google OAuth 2.0 dokümantasyonu",
                    Url = "https://developers.google.com/identity/protocols/oauth2/web-server",
                },
            ],
            Steps =
            [
                new OAuthSetupStep
                {
                    Order = 1,
                    Title = "Google Cloud projesi",
                    Detail = "Google Cloud Console'da yeni bir proje oluşturun veya mevcut projeyi seçin.",
                },
                new OAuthSetupStep
                {
                    Order = 2,
                    Title = "OAuth consent screen",
                    Detail = "APIs & Services → OAuth consent screen → User type: External. Uygulama adını girin. Test aşamasında kendi Gmail adresinizi Test users listesine ekleyin.",
                },
                new OAuthSetupStep
                {
                    Order = 3,
                    Title = "OAuth client ID oluşturun",
                    Detail = "Credentials → Create credentials → OAuth client ID → Application type: Web application.",
                },
                new OAuthSetupStep
                {
                    Order = 4,
                    Title = "Authorized redirect URI ekleyin",
                    Detail = $"Web client ayarlarında Authorized redirect URIs alanına tam olarak şunu ekleyin:\n{redirectUri}",
                },
                new OAuthSetupStep
                {
                    Order = 5,
                    Title = "Client ID ve Secret'ı kaydedin",
                    Detail = "Oluşturulan Client ID (*.apps.googleusercontent.com) ve Client secret değerlerini kopyalayın. Secret yalnızca bir kez gösterilir.",
                },
                new OAuthSetupStep
                {
                    Order = 6,
                    Title = "Sunucu yapılandırması",
                    Detail = "Docker kullanıyorsanız proje kökündeki .env dosyasına OAUTH_GOOGLE_CLIENT_ID ve OAUTH_GOOGLE_CLIENT_SECRET ekleyin. Yerel geliştirmede appsettings.Development.json içinde OAuth:google bölümünü doldurun.",
                },
                new OAuthSetupStep
                {
                    Order = 7,
                    Title = "API'yi yeniden başlatın",
                    Detail = "Yapılandırma değişikliklerinden sonra API konteynerini veya dotnet run sürecini yeniden başlatın, ardından bu sayfada tekrar «Google ile giriş yap» deneyin.",
                },
            ],
            ConfigKeys = BuildGoogleLoginConfigKeys(clientId, clientSecret, redirectUri),
        };
    }

    public static OAuthSetupGuide ForGoogleSearchConsole(IConfiguration config)
    {
        var redirectUri = config["GoogleSearchConsole:RedirectUri"]
            ?? "http://localhost:4200/auth/search-console/callback";
        var scClientId = config["GoogleSearchConsole:ClientId"];
        var oauthClientId = config["OAuth:google:ClientId"];
        var clientId = !string.IsNullOrWhiteSpace(scClientId) ? scClientId : oauthClientId;
        var scSecret = config["GoogleSearchConsole:ClientSecret"];
        var oauthSecret = config["OAuth:google:ClientSecret"];
        var clientSecret = !string.IsNullOrWhiteSpace(scSecret) ? scSecret : oauthSecret;

        return new OAuthSetupGuide
        {
            Provider = "google",
            Purpose = "search-console",
            Title = "Search Console OAuth yapılandırması eksik",
            Summary = "Google Search Console bağlantısı için OAuth Client ID tanımlanmamış. Genellikle giriş OAuth'u ile aynı client kullanılabilir; ikinci bir redirect URI eklemeniz yeterlidir.",
            RedirectUri = redirectUri,
            EnvFileHint = "GOOGLE_SC_CLIENT_ID boş bırakılırsa OAUTH_GOOGLE_CLIENT_ID kullanılır. Her iki redirect URI'yi de Google Cloud client'ta tanımlayın.",
            Links =
            [
                new OAuthSetupLink
                {
                    Label = "Google Cloud Console — Credentials",
                    Url = "https://console.cloud.google.com/apis/credentials",
                },
                new OAuthSetupLink
                {
                    Label = "Search Console API'yi etkinleştir",
                    Url = "https://console.cloud.google.com/apis/library/searchconsole.googleapis.com",
                },
                new OAuthSetupLink
                {
                    Label = "Search Console API dokümantasyonu",
                    Url = "https://developers.google.com/webmaster-tools/v1/how-tos/authorizing",
                },
            ],
            Steps =
            [
                new OAuthSetupStep
                {
                    Order = 1,
                    Title = "Search Console API'yi açın",
                    Detail = "Google Cloud projenizde Search Console API'yi etkinleştirin (APIs & Services → Library → Google Search Console API → Enable).",
                },
                new OAuthSetupStep
                {
                    Order = 2,
                    Title = "OAuth client (Web application)",
                    Detail = "Giriş için oluşturduğunuz Web client'ı kullanabilirsiniz veya ayrı bir client oluşturun.",
                },
                new OAuthSetupStep
                {
                    Order = 3,
                    Title = "Search Console redirect URI",
                    Detail = $"Authorized redirect URIs listesine şunu da ekleyin:\n{redirectUri}",
                },
                new OAuthSetupStep
                {
                    Order = 4,
                    Title = "Yapılandırma anahtarları",
                    Detail = "GoogleSearchConsole:ClientId / GOOGLE_SC_CLIENT_ID veya OAuth:google:ClientId / OAUTH_GOOGLE_CLIENT_ID. Secret için karşılık gelen ClientSecret alanları.",
                },
                new OAuthSetupStep
                {
                    Order = 5,
                    Title = "Bağlantıyı test edin",
                    Detail = "Önce Google ile giriş yapın, ardından «Google Search Console Bağla» butonuna tıklayın ve Google hesabınıza erişim izni verin.",
                },
            ],
            ConfigKeys = BuildSearchConsoleConfigKeys(clientId, clientSecret, redirectUri, scClientId, oauthClientId),
        };
    }

    public static OAuthSetupGuide ForGoogleMerchantCenter(IConfiguration config)
    {
        var redirectUri = config["Google:MerchantCenter:RedirectUri"]
            ?? "http://localhost:4200/auth/merchant-center/callback";
        var gmcClientId = config["Google:MerchantCenter:ClientId"];
        var oauthClientId = config["OAuth:google:ClientId"];
        var clientId = !string.IsNullOrWhiteSpace(gmcClientId) ? gmcClientId : oauthClientId;
        var gmcSecret = config["Google:MerchantCenter:ClientSecret"];
        var oauthSecret = config["OAuth:google:ClientSecret"];
        var clientSecret = !string.IsNullOrWhiteSpace(gmcSecret) ? gmcSecret : oauthSecret;

        return new OAuthSetupGuide
        {
            Provider = "google",
            Purpose = "merchant-center",
            Title = "Merchant Center OAuth yapılandırması eksik",
            Summary = "Merchant Center API bağlantısı için OAuth Client ID tanımlanmamış. Merchant API'yi etkinleştirin ve content scope ekleyin.",
            RedirectUri = redirectUri,
            EnvFileHint = "GOOGLE_GMC_CLIENT_ID boş bırakılırsa OAUTH_GOOGLE_CLIENT_ID kullanılır.",
            Links =
            [
                new OAuthSetupLink { Label = "Merchant API", Url = "https://console.cloud.google.com/apis/library/merchantapi.googleapis.com" },
                new OAuthSetupLink { Label = "Developer registration", Url = "https://developers.google.com/merchant/api/guides/quickstart/registration" },
                new OAuthSetupLink { Label = "Merchant Center Yardım", Url = "https://support.google.com/merchants/?hl=tr" },
            ],
            Steps =
            [
                new OAuthSetupStep { Order = 1, Title = "Merchant API'yi açın", Detail = "Google Cloud projenizde Merchant API'yi etkinleştirin." },
                new OAuthSetupStep { Order = 2, Title = "registerGcp", Detail = "Birincil Merchant Center hesabınızda GCP projesini registerGcp ile kaydedin." },
                new OAuthSetupStep { Order = 3, Title = "Redirect URI", Detail = $"Authorized redirect URIs:\n{redirectUri}" },
                new OAuthSetupStep { Order = 4, Title = "Scope", Detail = "https://www.googleapis.com/auth/content scope consent screen'de seçilmeli." },
                new OAuthSetupStep
                {
                    Order = 5,
                    Title = "Bağlantıyı test edin",
                    Detail = "Giriş yapın → /merchant-center → «Merchant Center'a Bağlan» → OAuth tamamlayın → GMC hesabı seçerek «Analiz Et» ile feed özeti ve product_performance_view verilerini doğrulayın.",
                },
            ],
            ConfigKeys =
            [
                new OAuthConfigKeyHint { AppsettingsKey = "Google:MerchantCenter:ClientId", EnvVariable = "GOOGLE_GMC_CLIENT_ID", Configured = !string.IsNullOrWhiteSpace(clientId) },
                new OAuthConfigKeyHint { AppsettingsKey = "Google:MerchantCenter:ClientSecret", EnvVariable = "GOOGLE_GMC_CLIENT_SECRET", Configured = !string.IsNullOrWhiteSpace(clientSecret) },
                new OAuthConfigKeyHint { AppsettingsKey = "Google:MerchantCenter:RedirectUri", EnvVariable = "GOOGLE_GMC_REDIRECT_URI", Configured = true },
            ],
        };
    }

    private static IList<OAuthConfigKeyHint> BuildGoogleLoginConfigKeys(
        string? clientId, string? clientSecret, string redirectUri)
    {
        return
        [
            new OAuthConfigKeyHint
            {
                AppsettingsKey = "OAuth:google:ClientId",
                EnvVariable = "OAUTH_GOOGLE_CLIENT_ID",
                Description = "OAuth Web client ID (*.apps.googleusercontent.com)",
                Configured = !string.IsNullOrWhiteSpace(clientId),
            },
            new OAuthConfigKeyHint
            {
                AppsettingsKey = "OAuth:google:ClientSecret",
                EnvVariable = "OAUTH_GOOGLE_CLIENT_SECRET",
                Description = "OAuth client secret",
                Configured = !string.IsNullOrWhiteSpace(clientSecret),
            },
            new OAuthConfigKeyHint
            {
                AppsettingsKey = "OAuth:google:RedirectUri",
                EnvVariable = "OAUTH_GOOGLE_REDIRECT_URI",
                Description = $"Redirect URI (varsayılan: {redirectUri})",
                Configured = true,
            },
        ];
    }

    private static IList<OAuthConfigKeyHint> BuildSearchConsoleConfigKeys(
        string? effectiveClientId,
        string? effectiveSecret,
        string redirectUri,
        string? scClientId,
        string? oauthClientId)
    {
        var usingFallback = string.IsNullOrWhiteSpace(scClientId) && !string.IsNullOrWhiteSpace(oauthClientId);
        return
        [
            new OAuthConfigKeyHint
            {
                AppsettingsKey = "GoogleSearchConsole:ClientId",
                EnvVariable = "GOOGLE_SC_CLIENT_ID",
                Description = usingFallback
                    ? "Boş — OAuth:google:ClientId kullanılıyor"
                    : "Search Console OAuth client ID",
                Configured = !string.IsNullOrWhiteSpace(effectiveClientId),
            },
            new OAuthConfigKeyHint
            {
                AppsettingsKey = "OAuth:google:ClientId",
                EnvVariable = "OAUTH_GOOGLE_CLIENT_ID",
                Description = "Alternatif / giriş OAuth client ID",
                Configured = !string.IsNullOrWhiteSpace(oauthClientId),
            },
            new OAuthConfigKeyHint
            {
                AppsettingsKey = "GoogleSearchConsole:ClientSecret",
                EnvVariable = "GOOGLE_SC_CLIENT_SECRET",
                Description = "Client secret (OAuth:google:ClientSecret yedek)",
                Configured = !string.IsNullOrWhiteSpace(effectiveSecret),
            },
            new OAuthConfigKeyHint
            {
                AppsettingsKey = "GoogleSearchConsole:RedirectUri",
                EnvVariable = "GOOGLE_SC_REDIRECT_URI",
                Description = $"Search Console callback (varsayılan: {redirectUri})",
                Configured = true,
            },
        ];
    }
}
