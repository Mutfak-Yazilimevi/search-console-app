using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core.Storage;

namespace SearchConsoleApp.Services.Storage;

/// <summary>
/// Local file system'de blob saklar. Dev ve küçük deployment'lar için.
///
/// Config:
///   "Blob:Local:RootPath"      = "App_Data/blobs"  (relatif veya absolute)
///   "Blob:Local:PublicBaseUrl" = "http://localhost:5000/blobs"
///
/// SearchConsoleApp.Web Program.cs static file middleware ile RootPath altındaki
/// dosyaları PublicBaseUrl'den serve eder.
///
/// Limitler:
/// - Multi-instance: dosya sistemi pod'lar arası paylaşılmaz → NFS/EFS gerek
/// - Backup: ayrı strateji (DB backup'tan farklı)
/// - Production'da S3'e geçilmesi önerilir
/// </summary>
public class LocalFileBlobStorage : IBlobStorage
{
    private readonly string _rootPath;
    private readonly string _publicBaseUrl;

    public LocalFileBlobStorage(IConfiguration config)
    {
        _rootPath = config["Blob:Local:RootPath"] ?? "App_Data/blobs";
        _publicBaseUrl = (config["Blob:Local:PublicBaseUrl"] ?? "/blobs").TrimEnd('/');

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ValidateKey(key);

        var path = GetPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);

        // contentType meta-data olarak ayrı dosyada (basit yaklaşım)
        await File.WriteAllTextAsync(path + ".meta", contentType, ct);

        return GetPublicUrl(key);
    }

    public async Task<BlobReadResult?> ReadAsync(string key, CancellationToken ct = default)
    {
        ValidateKey(key);
        var path = GetPath(key);
        if (!File.Exists(path)) return null;

        var contentType = File.Exists(path + ".meta")
            ? await File.ReadAllTextAsync(path + ".meta", ct)
            : "application/octet-stream";

        var fs = File.OpenRead(path);  // caller dispose eder
        return new BlobReadResult(fs, contentType, fs.Length);
    }

    public Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        ValidateKey(key);
        var path = GetPath(key);
        if (!File.Exists(path)) return Task.FromResult(false);

        File.Delete(path);
        if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        ValidateKey(key);
        return Task.FromResult(File.Exists(GetPath(key)));
    }

    public string GetPublicUrl(string key, TimeSpan? ttl = null)
    {
        // Local impl TTL desteklemez — statik path döner
        return $"{_publicBaseUrl}/{key.TrimStart('/')}";
    }

    private string GetPath(string key) => Path.Combine(_rootPath, key.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Path traversal koruması (`..`, absolute path).</summary>
    private static void ValidateKey(string key)
    {
        if (key.Contains("..") || Path.IsPathRooted(key))
            throw new ArgumentException("Invalid key: path traversal denied", nameof(key));
    }
}
