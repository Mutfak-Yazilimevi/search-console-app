using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SearchConsoleApp.Core;
using SearchConsoleApp.Core.Auditing;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Auditing;

/// <summary>
/// IEntityChangeNotifier impl — EfRepository'den gelen tüm entity insert/update/delete
/// olaylarını AuditLog'a yansıtır.
///
/// Filtreleme:
/// - Excluded types: AuditLog, DeviceSession (kendi tablosu yeterli, volume yüksek)
/// - Sensitive fields: PasswordHash, TokenHash, vb. — "***" olarak yazılır
/// - Tracker'dan boş changes geliyorsa update sessiz geçer
/// </summary>
public class AuditableEntityNotifier : IEntityChangeNotifier, IScopedService
{
    private static readonly HashSet<string> ExcludedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AuditLog",
        "DeviceSession",  // çok yüksek volume, ayrı tabloda zaten izleniyor
    };

    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "TokenHash",
        "RefreshTokenHash",
        "Fingerprint",
        "PrivateKey",
        "Secret",
    };

    // IAuditService → IRepository<AuditLog> → IEntityChangeNotifier (bu sınıf)
    // şeklinde dairesel bağımlılık oluşur. Constructor yerine lazy resolve ile
    // döngü kırılır. AuditLog ExcludedTypes'ta olduğu için sonsuz döngü olmaz.
    private readonly IServiceProvider _services;

    public AuditableEntityNotifier(IServiceProvider services) => _services = services;

    public async Task NotifyAsync<T>(
        EntityChangeType type,
        T entity,
        IReadOnlyDictionary<string, (object? Old, object? New)>? changes)
        where T : BaseEntity
    {
        var typeName = typeof(T).Name;
        if (ExcludedTypes.Contains(typeName)) return;

        var verb = type switch
        {
            EntityChangeType.Inserted => "create",
            EntityChangeType.Updated  => "update",
            EntityChangeType.Deleted  => "delete",
            _ => "unknown"
        };
        var action = $"{typeName.ToLowerInvariant()}.{verb}";

        // Change JSON — hassas alanları maskele
        string? changesJson = null;
        if (changes != null && changes.Count > 0)
        {
            var sanitized = new Dictionary<string, object?>();
            foreach (var (field, (oldVal, newVal)) in changes)
            {
                sanitized[field] = SensitiveFields.Contains(field)
                    ? new { old = "***", @new = "***" }
                    : new { old = oldVal, @new = newVal };
            }
            try
            {
                changesJson = JsonSerializer.Serialize(sanitized);
            }
            catch
            {
                // Karmaşık tip serialize edilemezse skip
                changesJson = "{\"_warning\":\"changes_serialization_failed\"}";
            }
        }

        var auditService = _services.GetRequiredService<IAuditService>();
        await auditService.LogAsync(new AuditEntry
        {
            Action = action,
            TargetType = typeName,
            TargetId = entity.Id,
            TargetEntityId = entity.EntityId,
            ChangesJson = changesJson,
        });
    }
}
