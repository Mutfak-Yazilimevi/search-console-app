using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Core.Domain.Outbox;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

namespace SearchConsoleApp.Services.Outbox;

/// <summary>
/// "webhook.*" mesajlarını HTTP POST ile gönderir.
///
/// Davranış:
/// - Content-Type: application/json
/// - Headers: OutboxMessage.HeadersJson'dan + sistem header'ları
/// - 2xx success → success
/// - 4xx (408, 429 hariç) → PermanentException (retry yok, dead'e at)
/// - 5xx, timeout, network → transient (retry)
///
/// İmza/güvenlik:
/// `X-Webhook-Signature` header'ı HMAC-SHA256 ile payload imzalanmış. Receiver
/// secret ile doğrular. Secret config'te: `Webhook:SigningSecret`.
///
/// Idempotency:
/// `X-Webhook-Event-Id` header'ı OutboxMessage.EntityId. Receiver bunu
/// yakalayıp aynı event'in iki kez işlenmesini engellemeli.
/// </summary>
public class WebhookOutboxHandler : IOutboxMessageHandler, IScopedService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    private readonly ILogger<WebhookOutboxHandler> _logger;

    public WebhookOutboxHandler(
        IHttpClientFactory httpClientFactory,
        Microsoft.Extensions.Configuration.IConfiguration config,
        ILogger<WebhookOutboxHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public bool CanHandle(string messageType) =>
        messageType.StartsWith("webhook.", StringComparison.OrdinalIgnoreCase);

    public async Task SendAsync(OutboxMessage message, CancellationToken ct)
    {
        using var http = _httpClientFactory.CreateClient("outbox-webhook");
        http.Timeout = TimeSpan.FromSeconds(30);

        using var req = new HttpRequestMessage(HttpMethod.Post, message.Target)
        {
            Content = new StringContent(message.Payload, Encoding.UTF8, "application/json")
        };

        // Sistem header'ları
        req.Headers.Add("X-Webhook-Event-Id", message.EntityId.ToString());
        req.Headers.Add("X-Webhook-Event-Type", message.MessageType);
        if (!string.IsNullOrEmpty(message.CorrelationId))
            req.Headers.Add("X-Correlation-Id", message.CorrelationId);

        // HMAC imza
        var secret = _config["Webhook:SigningSecret"];
        if (!string.IsNullOrEmpty(secret))
        {
            var signature = ComputeSignature(message.Payload, secret);
            req.Headers.Add("X-Webhook-Signature", signature);
        }

        // Kullanıcı header'ları (HeadersJson)
        if (!string.IsNullOrEmpty(message.HeadersJson))
        {
            try
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(message.HeadersJson);
                if (headers != null)
                {
                    foreach (var (k, v) in headers)
                    {
                        if (!req.Headers.TryAddWithoutValidation(k, v))
                        {
                            req.Content.Headers.TryAddWithoutValidation(k, v);
                        }
                    }
                }
            }
            catch (JsonException) { /* invalid headers, skip */ }
        }

        HttpResponseMessage res;
        try
        {
            res = await http.SendAsync(req, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Timeout — transient
            throw new InvalidOperationException("Webhook timeout", ex);
        }

        if (res.IsSuccessStatusCode) return;

        var body = await res.Content.ReadAsStringAsync(ct);
        var status = (int)res.StatusCode;

        // 4xx → kalıcı (receiver kabul etmedi, retry anlamsız)
        // İstisna: 408 Request Timeout, 425 Too Early, 429 Too Many Requests
        if (status >= 400 && status < 500
            && status != 408 && status != 425 && status != 429)
        {
            throw new OutboxPermanentException(
                $"Webhook 4xx response: {status} - {Truncate(body, 500)}");
        }

        // 5xx, 408, 429 → transient (retry)
        throw new InvalidOperationException(
            $"Webhook transient error: {status} - {Truncate(body, 500)}");
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s[..max] + "…" : s);
}
