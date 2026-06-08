namespace SearchConsoleApp.Services.Identity;

/// <summary>
/// IP adresinden coğrafi bilgi çıkarır.
///
/// Mevcut implementasyon: MaxMind GeoLite2 (ücretsiz, offline DB).
/// Database dosyası `App_Data/GeoLite2-City.mmdb` olarak gelir veya
/// config'te `GeoIp:DatabasePath` ile başka yer gösterilir.
///
/// Database yoksa NoOpGeoIpService devreye girer → null döner (graceful fallback).
/// GeoLite2 indirme: https://dev.maxmind.com/geoip/geolite2-free-geolocation-data
/// </summary>
public interface IGeoIpService
{
    GeoIpResult? Lookup(string? ipAddress);
}

public record GeoIpResult(string? CountryCode, string? CountryName, string? City);
