using SearchConsoleApp.Core.Auth;
using SearchConsoleApp.Core.Domain.Customers;

namespace SearchConsoleApp.Services.Auth;

/// <summary>
/// OAuth/OIDC ile login akışı.
///
/// Akış (Authorization Code, Backend-for-Frontend pattern):
///
/// 1. Frontend → GET /api/v1/public/auth/external/{provider}
///    → Backend provider authorize URL'i build eder, redirect döner
///
/// 2. Kullanıcı provider'da giriş yapar
///    → Provider callback URL'e code ile redirect
///
/// 3. Frontend → POST /api/v1/public/auth/external/callback
///    → Backend code'u token'a çevirir, user info çeker
///    → Mevcut ExternalLogin varsa: o customer'a login
///    → Yoksa: email match → mevcut Customer'a link veya yeni Customer oluştur
///    → AuthTokens döner (normal JWT akışı)
///
/// PKCE: zorunlu (state + code_verifier — CSRF + replay attack koruması).
///       Backend session/cache'te tutar.
///
/// 4. Linked accounts yönetimi: /api/v1/web/account/external/* (Web audience)
/// </summary>
public interface IExternalAuthService
{
    /// <summary>Authorize URL build et — frontend redirect için.</summary>
    Task<string> BuildAuthorizeUrlAsync(string provider, string returnUrl);

    /// <summary>Callback: code'u token'a çevir, kullanıcı oluştur/giriş yap.</summary>
    Task<AuthResult> HandleCallbackAsync(string provider, string code, string state,
                                          string? ip, string? userAgent);

    /// <summary>Login halindeyken yeni provider bağla (ikinci faktör değil, ek login yolu).</summary>
    Task LinkProviderAsync(long customerId, string provider, string code, string state);

    /// <summary>Bağlı provider'ı sil. Customer'ın en az bir login yolu kalmalı (parola veya başka provider).</summary>
    Task UnlinkProviderAsync(long customerId, string provider);

    Task<IList<ExternalLogin>> GetLinkedProvidersAsync(long customerId);
}
