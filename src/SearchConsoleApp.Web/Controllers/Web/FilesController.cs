using Microsoft.AspNetCore.Mvc;
using SearchConsoleApp.Core.RequestScope;
using SearchConsoleApp.Core.Storage;
using SearchConsoleApp.Web.Framework.Api;
using SearchConsoleApp.Web.Framework.Auditing;

namespace SearchConsoleApp.Web.Controllers.Web;

/// <summary>
/// Authenticated kullanıcı dosya upload/download.
/// Route: /api/v1/web/files/*
///
/// Key formatı: `customers/{customerId}/{filename}` — her kullanıcının
/// kendi namespace'i, başkasının dosyalarına erişemez.
///
/// Üretim notları:
/// - Boyut limiti: Kestrel + Form options (örn. 10MB)
/// - MIME validation: client-side değil, server-side
/// - Virus scan: ClamAV / büyük dosyalar için ayrı queue
/// - Image processing: ImageSharp ile resize/optimize (opsiyonel)
/// </summary>
public class FilesController : WebApiController
{
    private readonly IBlobStorage _storage;
    private readonly IRequestScope _scope;

    public FilesController(IBlobStorage storage, IRequestScope scope)
    {
        _storage = storage;
        _scope = scope;
    }

    /// <summary>Kullanıcının kendi dosyasını upload eder.</summary>
    [HttpPost("upload")]
    [Audit("file.upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]  // 10MB
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();
        if (file == null || file.Length == 0) return BadRequest(new { error = "Dosya gerekli." });

        // Allowed MIME types — server-side validation
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
        if (!allowed.Contains(file.ContentType))
            return BadRequest(new { error = $"Desteklenmeyen tip: {file.ContentType}" });

        var safeName = SanitizeFilename(file.FileName);
        var key = $"customers/{customerId}/{Guid.NewGuid():N}-{safeName}";

        await using var stream = file.OpenReadStream();
        var url = await _storage.SaveAsync(key, stream, file.ContentType);

        return Ok(new { key, url, size = file.Length });
    }

    /// <summary>Kullanıcının dosyasının (presigned) URL'ini döner.</summary>
    [HttpGet("url")]
    public IActionResult GetUrl([FromQuery] string key)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        // Kullanıcı sadece kendi namespace'indeki dosyaya erişebilir
        var prefix = $"customers/{customerId}/";
        if (!key.StartsWith(prefix)) return Forbid();

        var url = _storage.GetPublicUrl(key, TimeSpan.FromMinutes(15));
        return Ok(new { url });
    }

    [HttpDelete]
    [Audit("file.delete")]
    public async Task<IActionResult> Delete([FromQuery] string key)
    {
        if (_scope.CustomerId is not long customerId) return Unauthorized();

        var prefix = $"customers/{customerId}/";
        if (!key.StartsWith(prefix)) return Forbid();

        var deleted = await _storage.DeleteAsync(key);
        return Ok(new { deleted });
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ' ' }).ToArray();
        var clean = string.Concat(name.Where(c => !invalid.Contains(c)));
        return string.IsNullOrEmpty(clean) ? "file" : clean;
    }
}
