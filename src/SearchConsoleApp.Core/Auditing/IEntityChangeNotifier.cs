namespace SearchConsoleApp.Core.Auditing;

/// <summary>
/// Repository, entity değişimlerini bu interface üzerinden audit'e bildirir.
/// Implementasyon Services katmanında (AuditableEntityNotifier).
///
/// Core'da olması, Data'nın Services'e bağlanmasını engeller — bağımlılık
/// yönü doğru kalır: Data → Core ← Services.
///
/// Opsiyonel: DI'da kayıt yoksa repository no-op çalışır.
/// </summary>
public interface IEntityChangeNotifier
{
    Task NotifyAsync<T>(EntityChangeType type, T entity,
                        IReadOnlyDictionary<string, (object? Old, object? New)>? changes)
        where T : BaseEntity;
}

public enum EntityChangeType
{
    Inserted = 0,
    Updated = 1,
    Deleted = 2,
}
