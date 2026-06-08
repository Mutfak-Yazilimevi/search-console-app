using SearchConsoleApp.Core.Domain.Customers;

namespace SearchConsoleApp.Core.Auth;

/// <summary>
/// JWT üretici soyutlaması. Web.Framework somut implementasyonu sağlar.
///
/// SessionId opsiyonel — login akışında DeviceSession oluşturulduktan SONRA
/// üretilen JWT'ye eklenir. Frontend bu claim'i decode edip "mevcut oturum
/// hangisi" diye SessionsController'a sorabilir.
/// </summary>
public interface IJwtIssuer
{
    (string Token, DateTime ExpiresAt) IssueAccessToken(Customer customer, long? sessionId = null);
}
