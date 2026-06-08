using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SearchConsoleApp.Services.Inbox;
using SearchConsoleApp.Web.Framework.Api;

namespace SearchConsoleApp.Web.Controllers.Public;

/// <summary>
/// External webhook'ları idempotent şekilde kabul eden örnek controller.
/// Route: /api/v1/public/webhooks/*
///
/// Her provider için ayrı action — header doğrulaması, payload formatı
/// provider'a göre değişir. Burada generic örnek olarak Stripe pattern'i.
///
/// İmza doğrulaması ZORUNLU. Her provider'ın kendi imza standardı var:
/// - Stripe: Stripe-Signature header, HMAC-SHA256
/// - GitHub: X-Hub-Signature-256, HMAC-SHA256
/// - Twilio: X-Twilio-Signature, HMAC-SHA1 + URL+params
///
/// Bu controller pattern göstergesi — gerçek provider için resmi SDK
/// kullanmak en güvenlisi (Stripe.NET, Octokit, vs.).
/// </summary>
public class WebhooksController : PublicApiController
{
    private readonly IInbox _inbox;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(IInbox inbox, IConfiguration config, ILogger<WebhooksController> logger)
    {
        _inbox = inbox;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Generic JSON webhook receiver — örnek implementasyon.
    ///
    /// Provider'a özel header'lardan event ID ve tip çıkarılır.
    /// İmza doğrulanır → inbox.TryRecord → duplicate ise 200, yeni ise process.
    /// </summary>
    [HttpPost("{source}")]
    public async Task<IActionResult> Receive(string source)
    {
        // Raw body oku — imza doğrulama için
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();

        // İmza doğrula
        if (!VerifySignature(source, rawBody, Request.Headers))
        {
            _logger.LogWarning("Webhook signature invalid: source={Source}", source);
            return Unauthorized();
        }

        // Provider-specific event ID ve tip çıkar
        var (eventId, eventType) = ExtractEventMetadata(source, rawBody, Request.Headers);
        if (string.IsNullOrEmpty(eventId))
        {
            _logger.LogWarning("Webhook missing event ID: source={Source}", source);
            return BadRequest(new { error = "Missing event id" });
        }

        // Inbox'a kaydet
        var result = await _inbox.TryRecordAsync(source, eventId, eventType, rawBody);

        if (result.AlreadyProcessed)
        {
            // İdempotent — daha önce işlendi, 200 dön
            _logger.LogInformation("Webhook duplicate: source={Source}, eventId={EventId}", source, eventId);
            return Ok(new { ok = true, duplicate = true });
        }

        // Yeni event — business logic burada
        try
        {
            await ProcessEventAsync(source, eventType, rawBody);
            await _inbox.MarkProcessedAsync(result.Id);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            await _inbox.MarkFailedAsync(result.Id, ex.Message);
            _logger.LogError(ex, "Webhook processing failed: source={Source}, eventId={EventId}", source, eventId);

            // 500 dön ki provider retry yapsın. Inbox'ta failed olarak kalır,
            // tekrar gelirse "AlreadyProcessed" değil — yeniden işlenir
            // (failed durumunda race condition: tasarım kararı, status'a göre
            // yeniden işle veya değil — burada yeniden işleyelim).
            return StatusCode(500, new { error = "Processing failed" });
        }
    }

    private bool VerifySignature(string source, string rawBody, IHeaderDictionary headers)
    {
        var secret = _config[$"Webhooks:Receive:{source}:SigningSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            // Secret tanımlı değilse: dev'de bypass, prod'da reject
            // Burada strict mode — secret yoksa reject
            return false;
        }

        // Stripe pattern: header "t=<timestamp>,v1=<hmac>"
        // Generic örnek: "X-Webhook-Signature: sha256=<hmac>"
        var sigHeader = headers["X-Webhook-Signature"].ToString();
        if (string.IsNullOrEmpty(sigHeader)) return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = "sha256=" + Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();

        // Constant-time karşılaştırma
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(sigHeader),
            Encoding.UTF8.GetBytes(expected));
    }

    private (string EventId, string EventType) ExtractEventMetadata(
        string source, string rawBody, IHeaderDictionary headers)
    {
        // Provider-specific — burada generic örnek:
        // Header'dan event ID, body'den event type (JSON parse)
        var eventId = headers["X-Webhook-Event-Id"].ToString();
        if (string.IsNullOrEmpty(eventId)) return ("", "");

        // Type için body parse — ucuza
        string eventType = "unknown";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
            if (doc.RootElement.TryGetProperty("type", out var t))
                eventType = t.GetString() ?? "unknown";
        }
        catch (System.Text.Json.JsonException) { /* invalid JSON, type=unknown */ }

        return (eventId, eventType);
    }

    private Task ProcessEventAsync(string source, string eventType, string payload)
    {
        // Gerçek implementasyonda: handler pattern (IEventHandler<T> dispatch),
        // veya switch case event type'a göre. Burada placeholder.
        _logger.LogInformation("Processing webhook: source={Source}, type={Type}", source, eventType);
        return Task.CompletedTask;
    }
}
