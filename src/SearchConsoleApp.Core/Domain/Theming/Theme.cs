using SearchConsoleApp.Core;

namespace SearchConsoleApp.Core.Domain.Theming;

/// <summary>
/// Tenant'a özel veya global tema. Frontend tarafındaki Theme JSON şemasıyla
/// uyumlu — JSON içeriği `JsonContent` olarak saklanır (esneklik için).
///
/// Multi-tenant aktifse, Tenant'a özel tema `TenantId` ile filtrelenir.
/// Şu an `ITenantScoped` implement ETMİYOR — açıkça istendiğinde eklenir
/// (docs/MULTI_TENANCY.md).
/// </summary>
public partial class Theme : BaseEntity, ISoftDeletable
{
    /// <summary>Tema ismi (slug), URL'de kullanılır. Ör: "acme-light"</summary>
    public string Name { get; set; } = "";

    /// <summary>Gösterim ismi, ör: "Acme Corp"</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>'light' | 'dark'</summary>
    public string Mode { get; set; } = "light";

    /// <summary>
    /// Tam tema JSON'u (colors, fonts, radius, shadow). Frontend'in Theme
    /// interface'iyle 1:1 uyumlu. Hot reload için JSON tutmak en pratik —
    /// her renk için ayrı kolon abartı olurdu.
    /// </summary>
    public string JsonContent { get; set; } = "{}";

    /// <summary>Aktif mi (publish edilmiş mi)?</summary>
    public bool Active { get; set; }

    /// <summary>Soft delete</summary>
    public bool Deleted { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}
