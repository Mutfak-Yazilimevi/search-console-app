namespace SearchConsoleApp.Core.Storage;

/// <summary>
/// Dosya/blob saklama soyutlaması.
///
/// İki gerçek impl:
/// - `LocalFileBlobStorage` (dev / küçük deployment): App_Data altında dosya
/// - `S3BlobStorage` (production): AWS S3, MinIO, R2, DigitalOcean Spaces — S3-uyumlu
///
/// Key formatı: hiyerarşik path, /  ayraç (S3 ile uyumlu).
///   "customers/42/avatar.jpg"
///   "themes/dark/preview.png"
///
/// Public URL dönüşümü:
/// - Local: appsettings'teki PublicBaseUrl + key
/// - S3: presigned URL veya CDN URL
///
/// Multi-tenant: tenant prefix'i caller'ın sorumluluğunda (key'e ekle).
/// </summary>
public interface IBlobStorage
{
    /// <summary>Stream'i kaydet, public URL'i veya identifier'ı dön.</summary>
    Task<string> SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Stream olarak oku. Yoksa null.</summary>
    Task<BlobReadResult?> ReadAsync(string key, CancellationToken ct = default);

    Task<bool> DeleteAsync(string key, CancellationToken ct = default);

    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Public URL üret. S3 impl presigned URL döner (TTL'li),
    /// local impl statik path döner.
    /// </summary>
    string GetPublicUrl(string key, TimeSpan? ttl = null);
}

public record BlobReadResult(Stream Content, string ContentType, long Length);
