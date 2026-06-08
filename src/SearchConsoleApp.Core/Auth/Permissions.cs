namespace SearchConsoleApp.Core.Auth;

/// <summary>
/// Permission'lar string sabit olarak tanımlı — DB'ye yazılmaz, kod-tanımlı.
///
/// Format: `{resource}.{action}`. Örnek: "customers.update", "audit.read".
///
/// Çoğu uygulamada bu yaklaşım yeterli ve performant:
/// - Kod tabanında "neler var" görebiliyorsun (DB'ye bakman gerekmez)
/// - Compile-time kontrol (typo'ları C# yakalar)
/// - Yeni permission eklemek = sabit ekle + role mapping güncelle
///
/// Çok dinamik permission gerekirse (her customer kendi permission setini
/// yönetir): DB-driven yaklaşıma geçilir. Şimdilik sabit liste yeterli.
/// </summary>
public static class Permissions
{
    // === Customers ===
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";
    public const string CustomersDelete = "customers.delete";

    // === Themes ===
    public const string ThemesRead = "themes.read";
    public const string ThemesWrite = "themes.write";
    public const string ThemesDelete = "themes.delete";

    // === Audit ===
    public const string AuditRead = "audit.read";

    // === Sessions ===
    public const string SessionsReadAny = "sessions.read.any";       // herhangi bir kullanıcının
    public const string SessionsRevokeAny = "sessions.revoke.any";

    // === System ===
    public const string SystemSettings = "system.settings";
    public const string SystemHealth = "system.health";

    /// <summary>Tüm permission'lar — yardımcı (örn. super-admin için "tümü").</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        CustomersRead, CustomersWrite, CustomersDelete,
        ThemesRead, ThemesWrite, ThemesDelete,
        AuditRead,
        SessionsReadAny, SessionsRevokeAny,
        SystemSettings, SystemHealth,
    };
}

/// <summary>
/// Role → permission'lar mapping.
///
/// Customer.Roles "user,admin" gibi virgülle ayrılı. Login sırasında bu
/// rollerin birleşim seti permission'lar JWT'ye claim olarak ekleniyor.
///
/// "user" rolü hiçbir admin permission'ı vermez — Web audience'a giriş
/// imkanı yeterli (kendi profilini görebilir, başkasının değil).
///
/// "admin": şu an tüm permission'lar — production'da fine-grained roller
/// eklenebilir (ör. "support-agent" sadece read, "operator" sadece sessions).
/// </summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, IReadOnlyList<string>> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // "user" → sıradan üye, kendi datasına erişir (controller-level + IRequestScope)
        ["user"] = Array.Empty<string>(),

        // "admin" → tüm yetkiler
        ["admin"] = Permissions.All,

        // Örnek dar yetkili roller (production'da genişlet):
        ["support-agent"] = new[]
        {
            Permissions.CustomersRead,
            Permissions.AuditRead,
            Permissions.SessionsReadAny,
        },
        ["operator"] = new[]
        {
            Permissions.SessionsReadAny,
            Permissions.SessionsRevokeAny,
            Permissions.SystemHealth,
        },
    };

    public static IReadOnlyList<string> ResolveForRoles(string rolesCsv)
    {
        if (string.IsNullOrWhiteSpace(rolesCsv)) return Array.Empty<string>();

        var roles = rolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim());

        var set = new HashSet<string>();
        foreach (var role in roles)
        {
            if (Map.TryGetValue(role, out var perms))
            {
                foreach (var p in perms) set.Add(p);
            }
        }
        return set.ToList();
    }
}
