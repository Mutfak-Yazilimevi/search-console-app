using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;
using SearchConsoleApp.Data;
using SearchConsoleApp.Core.Domain.Notifications;

namespace SearchConsoleApp.Services.Notifications;

/// <summary>
/// Expo Push API üzerinden push notification gönderir.
/// Docs: https://docs.expo.dev/push-notifications/sending-notifications/
///
/// Kullanım: backend, kullanıcı action'ına (yeni mesaj, sipariş, vb.) tepki
/// olarak `SendToCustomerAsync(...)` çağırır. Expo bizim için APNs/FCM'ye
/// yönlendirir — Apple/Google credentials'ı yönetme derdi yok.
///
/// Tek limit: Expo'ya kayıtlı mobile app gerekli. Bare CLI'de native APNs/FCM
/// kullanmak istersek bu service'i değiştirmek yeter, public API aynı kalır.
/// </summary>
public partial class ExpoPushNotificationService : INotificationService, IScopedService
{
    private const string ExpoPushUrl = "https://exp.host/--/api/v2/push/send";
    private const int BatchSize = 100;  // Expo limit

    private readonly IRepository<DeviceToken> _tokenRepo;
    private readonly HttpClient _http;
    private readonly ILogger<ExpoPushNotificationService> _logger;

    public ExpoPushNotificationService(
        IRepository<DeviceToken> tokenRepo,
        IHttpClientFactory httpFactory,
        ILogger<ExpoPushNotificationService> logger)
    {
        _tokenRepo = tokenRepo;
        _http = httpFactory.CreateClient("expo-push");
        _logger = logger;
    }

    public virtual async Task<NotificationResult> SendToCustomerAsync(long customerId, PushMessage message)
    {
        var tokens = await _tokenRepo.Table
            .Where(t => t.CustomerId == customerId && t.Provider == "expo")
            .Select(t => t.Token)
            .ToListAsync();

        return await SendToTokensAsync(tokens, message);
    }

    public virtual async Task<NotificationResult> SendToCustomersAsync(IEnumerable<long> customerIds, PushMessage message)
    {
        var ids = customerIds.ToList();
        var tokens = await _tokenRepo.Table
            .Where(t => ids.Contains(t.CustomerId) && t.Provider == "expo")
            .Select(t => t.Token)
            .ToListAsync();

        return await SendToTokensAsync(tokens, message);
    }

    public virtual async Task<NotificationResult> SendToTokensAsync(IEnumerable<string> expoTokens, PushMessage message)
    {
        var validTokens = expoTokens
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.StartsWith("ExponentPushToken["))
            .Distinct()
            .ToList();

        if (validTokens.Count == 0)
            return new NotificationResult(0, 0, Array.Empty<string>());

        var invalidTokens = new List<string>();
        var sent = 0;
        var failed = 0;

        // Expo batch limiti: 100 mesaj/istek
        foreach (var batch in validTokens.Chunk(BatchSize))
        {
            var payloads = batch.Select(token => new ExpoPushPayload
            {
                To = token,
                Title = message.Title,
                Body = message.Body,
                Data = message.Data,
                Sound = message.Sound,
                Badge = message.Badge,
                ChannelId = message.ChannelId,
            }).ToList();

            try
            {
                var response = await _http.PostAsJsonAsync(ExpoPushUrl, payloads);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<ExpoPushResponse>();
                if (result?.Data != null)
                {
                    for (int i = 0; i < result.Data.Count && i < batch.Length; i++)
                    {
                        if (result.Data[i].Status == "ok")
                        {
                            sent++;
                        }
                        else
                        {
                            failed++;
                            // DeviceNotRegistered → token geçersiz, DB'den temizlenmeli
                            if (result.Data[i].Details?.Error == "DeviceNotRegistered")
                                invalidTokens.Add(batch[i]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Expo push gönderimi başarısız (batch size: {Size})", batch.Length);
                failed += batch.Length;
            }
        }

        // Geçersiz token'ları DB'den temizle (Expo söyledi, cihaz uninstall etmiş)
        if (invalidTokens.Count > 0)
        {
            var stale = await _tokenRepo.Table
                .Where(t => invalidTokens.Contains(t.Token))
                .ToListAsync();
            foreach (var t in stale) await _tokenRepo.DeleteAsync(t, publishEvent: false);
        }

        return new NotificationResult(sent, failed, invalidTokens);
    }

    // === Expo API payload tipleri ===

    private class ExpoPushPayload
    {
        [JsonPropertyName("to")] public string To { get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("body")] public string Body { get; set; } = "";
        [JsonPropertyName("data")] public Dictionary<string, object>? Data { get; set; }
        [JsonPropertyName("sound")] public string? Sound { get; set; }
        [JsonPropertyName("badge")] public int? Badge { get; set; }
        [JsonPropertyName("channelId")] public string? ChannelId { get; set; }
    }

    private class ExpoPushResponse
    {
        [JsonPropertyName("data")] public List<ExpoPushTicket>? Data { get; set; }
    }

    private class ExpoPushTicket
    {
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("details")] public ExpoPushErrorDetails? Details { get; set; }
    }

    private class ExpoPushErrorDetails
    {
        [JsonPropertyName("error")] public string? Error { get; set; }
    }
}
