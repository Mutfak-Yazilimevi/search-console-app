using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using SearchConsoleApp.Core.Storage;

namespace SearchConsoleApp.Services.Storage;

/// <summary>
/// AWS S3 ve S3-uyumlu sağlayıcılar (MinIO, Cloudflare R2, DigitalOcean
/// Spaces, Wasabi, Backblaze B2).
///
/// Config:
///   "Blob:S3:BucketName"      = "SearchConsoleApp-blobs"
///   "Blob:S3:Region"          = "eu-central-1"
///   "Blob:S3:ServiceUrl"      = ""        (boş = AWS, dolu = S3-uyumlu)
///   "Blob:S3:AccessKey"       = "..."
///   "Blob:S3:SecretKey"       = "..."
///   "Blob:S3:PublicBaseUrl"   = "https://cdn.SearchConsoleApp.com"   (opsiyonel, CDN için)
///   "Blob:S3:PresignedUrlTtlMinutes" = 60
///
/// Production önerisi: AWS credentials env var veya IAM role ile —
/// appsettings'e gömme.
///
/// Public URL stratejisi:
/// - PublicBaseUrl varsa → static URL (CDN arkasında)
/// - Yoksa → presigned URL (TTL'li)
/// </summary>
public class S3BlobStorage : IBlobStorage, IDisposable
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly string? _publicBaseUrl;
    private readonly TimeSpan _defaultTtl;

    public S3BlobStorage(IConfiguration config)
    {
        var section = config.GetSection("Blob:S3");
        _bucket = section["BucketName"]
            ?? throw new InvalidOperationException("Blob:S3:BucketName eksik.");

        var region = section["Region"] ?? "us-east-1";
        var serviceUrl = section["ServiceUrl"];
        var accessKey = section["AccessKey"];
        var secretKey = section["SecretKey"];

        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region),
        };

        // S3-uyumlu sağlayıcılar (MinIO, R2, Spaces) için ServiceUrl override
        if (!string.IsNullOrEmpty(serviceUrl))
        {
            s3Config.ServiceURL = serviceUrl;
            s3Config.ForcePathStyle = true;  // MinIO için gerekli
        }

        _s3 = !string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey)
            ? new AmazonS3Client(accessKey, secretKey, s3Config)
            : new AmazonS3Client(s3Config);  // IAM role / env vars

        _publicBaseUrl = section["PublicBaseUrl"]?.TrimEnd('/');
        _defaultTtl = TimeSpan.FromMinutes(int.Parse(section["PresignedUrlTtlMinutes"] ?? "60"));
    }

    public async Task<string> SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var req = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        };

        await _s3.PutObjectAsync(req, ct);
        return GetPublicUrl(key);
    }

    public async Task<BlobReadResult?> ReadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var res = await _s3.GetObjectAsync(_bucket, key, ct);
            return new BlobReadResult(res.ResponseStream, res.Headers.ContentType, res.ContentLength);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _s3.DeleteObjectAsync(_bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _s3.GetObjectMetadataAsync(_bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public string GetPublicUrl(string key, TimeSpan? ttl = null)
    {
        // CDN/PublicBaseUrl varsa: doğrudan URL (objects public read olmalı)
        if (!string.IsNullOrEmpty(_publicBaseUrl))
        {
            return $"{_publicBaseUrl}/{key.TrimStart('/')}";
        }

        // Yoksa: presigned URL (TTL'li, geçici erişim)
        var req = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(ttl ?? _defaultTtl),
            Verb = HttpVerb.GET,
        };
        return _s3.GetPreSignedURL(req);
    }

    public void Dispose() => _s3?.Dispose();
}
