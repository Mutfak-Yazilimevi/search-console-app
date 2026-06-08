using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SearchConsoleApp.Core.Storage;
using SearchConsoleApp.Services.Storage;

namespace SearchConsoleApp.Web.Framework.Storage;

public static class BlobStorageSetup
{
    /// <summary>
    /// Blob storage'ı config'e göre kaydeder.
    ///
    /// "Blob:Provider" = "local" | "s3"
    ///
    /// local: LocalFileBlobStorage (App_Data/blobs)
    /// s3:    S3BlobStorage (AWS S3 / MinIO / R2 / Spaces)
    ///
    /// Marker pattern kullanılmadı çünkü iki impl de IBlobStorage —
    /// runtime'da config'e göre seçilir.
    /// </summary>
    public static IServiceCollection AddSearchConsoleAppBlobStorage(this IServiceCollection services, IConfiguration config)
    {
        var provider = config["Blob:Provider"]?.ToLowerInvariant() ?? "local";

        if (provider == "s3")
        {
            services.AddSingleton<IBlobStorage, S3BlobStorage>();
        }
        else
        {
            services.AddSingleton<IBlobStorage, LocalFileBlobStorage>();
        }

        return services;
    }

    /// <summary>
    /// Local blob storage için static file middleware.
    /// `/blobs` URL'i altında App_Data/blobs içeriğini serve eder.
    ///
    /// Sadece local provider için anlamlı — s3 modunda no-op.
    /// </summary>
    public static IApplicationBuilder UseSearchConsoleAppLocalBlobs(this IApplicationBuilder app, IConfiguration config)
    {
        var provider = config["Blob:Provider"]?.ToLowerInvariant() ?? "local";
        if (provider != "local") return app;

        var rootPath = config["Blob:Local:RootPath"] ?? "App_Data/blobs";
        var publicUrl = config["Blob:Local:PublicBaseUrl"] ?? "/blobs";

        // Relatif path'i absolute'a çevir
        if (!Path.IsPathRooted(rootPath))
        {
            rootPath = Path.Combine(Directory.GetCurrentDirectory(), rootPath);
        }
        Directory.CreateDirectory(rootPath);

        // PublicBaseUrl'in path kısmı (örn. "/blobs")
        var requestPath = new Uri(publicUrl, UriKind.RelativeOrAbsolute).IsAbsoluteUri
            ? new Uri(publicUrl).AbsolutePath
            : publicUrl;

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(rootPath),
            RequestPath = requestPath.TrimEnd('/'),
            ServeUnknownFileTypes = false,
        });

        return app;
    }
}
