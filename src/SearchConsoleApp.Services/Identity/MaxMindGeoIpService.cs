using System.Net;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Identity;

/// <summary>
/// MaxMind GeoLite2 database reader. Singleton — DB dosyası bir kez yüklenir.
///
/// Database yoksa NoOpGeoIpService devreye alınır. Bu sınıf yine de kayıtlı
/// olur ama Lookup null döner — caller crash etmez, sadece IpCountry/City boş kalır.
///
/// Production'a indirmek için: GeoLite2-City.mmdb dosyasını
/// `App_Data/` veya config'te belirttiğin yere koy.
/// </summary>
public class MaxMindGeoIpService : IGeoIpService, ISingletonService, IDisposable
{
    private readonly DatabaseReader? _reader;
    private readonly ILogger<MaxMindGeoIpService> _logger;

    public MaxMindGeoIpService(IConfiguration config, ILogger<MaxMindGeoIpService> logger)
    {
        _logger = logger;
        var dbPath = config["GeoIp:DatabasePath"] ?? "App_Data/GeoLite2-City.mmdb";

        if (!File.Exists(dbPath))
        {
            _logger.LogWarning(
                "GeoIP database bulunamadı: {Path}. GeoIp lookup null dönecek. " +
                "GeoLite2'yi https://dev.maxmind.com/geoip/geolite2-free-geolocation-data adresinden indir.",
                dbPath);
            _reader = null;
            return;
        }

        try
        {
            _reader = new DatabaseReader(dbPath);
            _logger.LogInformation("GeoIP database yüklendi: {Path}", dbPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeoIP database yüklenemedi: {Path}", dbPath);
            _reader = null;
        }
    }

    public GeoIpResult? Lookup(string? ipAddress)
    {
        if (_reader == null) return null;
        if (string.IsNullOrWhiteSpace(ipAddress)) return null;

        // Loopback ve özel IP'leri atla
        if (!IPAddress.TryParse(ipAddress, out var ip)) return null;
        if (IPAddress.IsLoopback(ip)) return null;
        if (IsPrivateNetwork(ip)) return null;

        try
        {
            var city = _reader.City(ip);
            return new GeoIpResult(
                CountryCode: city.Country?.IsoCode,
                CountryName: city.Country?.Name,
                City: city.City?.Name);
        }
        catch (AddressNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GeoIP lookup hatası: {Ip}", ipAddress);
            return null;
        }
    }

    private static bool IsPrivateNetwork(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return false;  // IPv6 lookup MaxMind'in kendisi yapar

        return bytes[0] == 10                                  // 10.0.0.0/8
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)  // 172.16.0.0/12
            || (bytes[0] == 192 && bytes[1] == 168)            // 192.168.0.0/16
            || (bytes[0] == 169 && bytes[1] == 254);           // link-local
    }

    public void Dispose() => _reader?.Dispose();
}
