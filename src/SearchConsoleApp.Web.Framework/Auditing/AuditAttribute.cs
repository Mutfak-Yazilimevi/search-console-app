namespace SearchConsoleApp.Web.Framework.Auditing;

/// <summary>
/// Controller action'ını audit log'a otomatik düşürür.
///
/// Kullanım:
///   [HttpPost("login")]
///   [Audit("auth.login")]
///   public async Task&lt;IActionResult&gt; Login(...) { ... }
///
/// Action başarılı (2xx) tamamlanırsa Outcome=success, hata (4xx/5xx) ise
/// Outcome=failure olarak log'lanır.
///
/// Manuel entity change'lerinden farkı: bu attribute "ne yapıldı" anlamında
/// semantic action ismi taşır — "customer.update" değil "customer.password_reset"
/// gibi domain-aware isimler için.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AuditAttribute : Attribute
{
    public string Action { get; }

    /// <summary>İlişkili entity tipi. Null ise sadece action log'lanır (örn. "auth.login").</summary>
    public string? TargetType { get; init; }

    /// <summary>Route parametresinde target id arar (örn. "entityId").</summary>
    public string? TargetIdRouteKey { get; init; }

    public AuditAttribute(string action) => Action = action;
}
