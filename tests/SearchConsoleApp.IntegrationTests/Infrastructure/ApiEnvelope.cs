using System.Net.Http.Json;

namespace SearchConsoleApp.IntegrationTests.Infrastructure;

/// <summary>
/// API tüm başarılı yanıtları ApiResponse&lt;T&gt; zarfıyla döner:
/// { "success": true, "data": {...}, "message": null }.
/// Test'ler gerçek payload'a ulaşmak için bu zarfı açmalı.
/// </summary>
public record ApiEnvelope<T>(bool Success, T? Data, string? Message);

public static class HttpEnvelopeExtensions
{
    /// <summary>Başarılı yanıtın zarfını açar ve Data'yı döner.</summary>
    public static async Task<T?> ReadEnvelopeDataAsync<T>(this HttpContent content)
    {
        var env = await content.ReadFromJsonAsync<ApiEnvelope<T>>();
        return env is null ? default : env.Data;
    }
}
