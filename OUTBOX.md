# Outbox, Feature Flags, GDPR

Bu doküman v17'de eklenen üç production-grade özelliği açıklar:

## 1. Webhook Outbox Pattern

### Problem

Senaryo: Order yaratıldı → Stripe webhook gönder. Düz `httpClient.PostAsync` riski:
- Network hatası → kayıp
- Üçüncü taraf down → kayıp
- Process crash → kayıp
- Business commit oldu ama webhook gitmedi → tutarsızlık

### Çözüm

Transactional outbox:

```
[Business Action]              [Outbox Worker]
       │                              │
       ▼                              ▼
   ┌──────────┐                  ┌──────────┐
   │  Order   │                  │ Outbox   │
   │  insert  │ ─── aynı tx ───▶│ insert   │
   └──────────┘                  └──────────┘
                                       │
                                       ▼
                              [OutboxDispatcherService]
                                       │
                                       ▼
                              [WebhookOutboxHandler]
                                       │
                                       ▼
                                 [Stripe API]
```

**At-least-once delivery garantisi:**
- Business commit edildi mi? → outbox kaydı da var (aynı tx)
- Process crash olsa bile mesaj DB'de
- Retry + exponential backoff
- Dead letter (8 deneme sonrası)

### Kullanım

```csharp
public class OrderService(IOutbox outbox, IRepository<Order> orders)
{
    public async Task CreateAsync(Order order)
    {
        await orders.InsertAsync(order);

        // Aynı request scope DbContext = aynı transaction
        await outbox.EnqueueAsync(new OutboxEnqueue
        {
            MessageType = "webhook.order.created",
            Target = "https://merchant.example.com/webhooks/orders",
            Payload = JsonSerializer.Serialize(new {
                orderId = order.EntityId,
                amount = order.Total,
            }),
            Headers = new() { ["X-Merchant-Id"] = "abc-123" }
        });
    }
}
```

### Webhook Security

Receiver `X-Webhook-Signature` header'ı doğrulamalı:

```
X-Webhook-Event-Id: 0123456789abcdef-...
X-Webhook-Event-Type: webhook.order.created
X-Webhook-Signature: sha256=hmac_of_payload_with_secret
```

HMAC-SHA256 ile `Webhook:SigningSecret` kullanılır. Receiver tarafı:

```javascript
const expected = 'sha256=' + crypto
  .createHmac('sha256', SHARED_SECRET)
  .update(rawBody)
  .digest('hex');

if (req.headers['x-webhook-signature'] !== expected) {
  return res.status(401).send('Invalid signature');
}
```

### Idempotency

`X-Webhook-Event-Id` = OutboxMessage.EntityId. Aynı event tekrar gelirse (retry sırasında receiver'a varmış ama 5xx döndüğü için bilmiyor), receiver bu ID'yi yakalayıp **idempotent** işlemeli:

```javascript
const eventId = req.headers['x-webhook-event-id'];
if (await alreadyProcessed(eventId)) return res.status(200).send();
await processOrder(req.body);
await markProcessed(eventId);
```

### Retry & Dead Letter

| Attempt | Backoff |
|---|---|
| 1 (ilk) | hemen |
| 2 | 30s sonra |
| 3 | 2dk sonra |
| 4 | 10dk sonra |
| 5 | 1h sonra |
| 6 | 6h sonra |
| 7-8 | 24h sonra |
| 9+ | dead-letter |

**Kalıcı hata** (`OutboxPermanentException`): 4xx HTTP response (408, 425, 429 hariç) — retry anlamsız, hemen dead'e.

### Monitoring

Admin endpoint:
- `GET /api/v1/admin/outbox?status=dead&take=100` — dead-letter listesi
- `POST /api/v1/admin/outbox/{id}/retry` — manuel retry
- `DELETE /api/v1/admin/outbox/{id}` — kalıcı sil

Permission: `system.settings`.

### Config

```json
"Outbox": {
  "PollIntervalSeconds": 5,
  "BatchSize": 50,
  "MaxAttempts": 8,
  "ClaimTimeoutMinutes": 5
},
"Webhook": {
  "SigningSecret": "..."   // env var önerilir
}
```

### Multi-instance

Dispatcher claim mekanizması atomic `ExecuteUpdateAsync` ile race condition korunmalı:

```sql
UPDATE OutboxMessage
SET Status='in_progress', LastAttemptUtc=now
WHERE Id IN (...pendingIds) AND Status='pending'
```

İki worker aynı row'u alamaz — biri kazanır.

Stuck `in_progress` mesajlar (process crash sonrası): `LastAttemptUtc < now - 5dk` → otomatik pending'e dönüş.

---

## 2. Feature Flags

OpenFeature standardına uyumlu ince wrapper — provider-agnostic.

### Kullanım

```csharp
public class CheckoutController(IFeatureFlags flags)
{
    public async Task<IActionResult> Checkout()
    {
        if (await flags.IsEnabledAsync(FeatureFlagKeys.NewCheckoutFlow))
        {
            return new NewCheckoutFlow();
        }
        return new LegacyCheckoutFlow();
    }
}
```

### Targeting (in-process impl)

```json
"FeatureFlags": {
  "maintenance-mode": false,

  "beta-admin-ui": {
    "default": false,
    "rolloutPercent": 10,
    "enabledForRoles": ["admin"],
    "enabledForCustomerIds": [42, 88]
  }
}
```

**Priority:**
1. `enabledForCustomerIds` match → açık
2. `enabledForRoles` match → açık
3. `rolloutPercent > 0` + customer hash bucket → açık
4. `default` değer

**Sticky bucketing:** Aynı customer + aynı flag = aynı bucket. Rollout %10 → bucket 0-9 alanlar her zaman açık görür (consistent UX).

### Maintenance Mode

`MaintenanceMiddleware` `maintenance-mode` flag'i açıkken admin olmayan herkese **503 + Retry-After** döner. Health endpoint'leri hariç (k8s probe'ları çalışmaya devam etmeli).

### Provider Migration

In-process implementation appsettings okur. Yönetilen servise geçiş (LaunchDarkly, Unleash, ConfigCat) için:

1. NuGet paketi ekle (ör. `LaunchDarkly.ServerSdk`)
2. Yeni `IFeatureFlags` impl yaz
3. DI'da swap et — `IScopedService` yerine manuel registration

Service'lerin kodu **değişmez** — interface aynı.

---

## 3. GDPR / KVKK — Right to be Forgotten

### Strateji: Anonymize, Not Delete

Customer hard-delete edersek:
- Audit log'larda orphan ID — "kim yaptı bilinmiyor"
- Hukuki kayıt gereksinimi (KVKK 7/2) ihlali

Çözüm: **PII'yi temizle, kaydı sakla**.

```
Customer.Email:     alice@x.com    → deleted-42@anonymized.local
Customer.FirstName: Alice          → null
Customer.PasswordHash:              → null
Customer.Active:                    → false
Customer.Deleted:                   → true (ISoftDeletable)

AuditLog.ActorEmail: alice@x.com   → null
AuditLog.ActorIp:    1.2.3.4       → null
AuditLog.Action:    "order.create" → KALIR (hukuki kanıt)

DeviceSession.IpAddress, UserAgent → null
DeviceSession.IpCountry             → KALIR (anonim istatistik)

Device:                             → HARD DELETE (fingerprint PII)
ExternalLogin:                      → HARD DELETE (provider mapping PII)
```

### Endpoint'ler

**Kullanıcı kendisi:**
- `GET /api/v1/web/account/privacy/export` — JSON indir
- `POST /api/v1/web/account/privacy/delete { password, reason }` — anonymize

OAuth-only kullanıcı parola yoksa → "önce parola belirle veya admin'le iletişime geç" (parola doğrulaması GDPR delete için zorunlu, kaza önleme).

**Admin (permission: customers.delete):**
- `GET /api/v1/admin/privacy/customers/{id}/export`
- `POST /api/v1/admin/privacy/customers/{id}/anonymize { reason }`

### Audit Trail

`gdpr.self_delete` veya `admin.gdpr.anonymize` audit kaydı düşer:
- Kim talep etti
- Ne zaman
- Reason (string field)

Bu kayıtlar hukuki gereksinim — GDPR delete işleminin **kendisi** audit'lenir.

### Data Export Format

JSON:
```json
{
  "customer": { "email": "...", "createdOnUtc": "...", "twoFactorEnabled": true },
  "externalLogins": [{ "provider": "google", "linkedOnUtc": "..." }],
  "devices": [{ "name": "MacBook", "firstSeenUtc": "..." }],
  "sessions": [{ "audience": "web", "ipCountry": "TR", "startedUtc": "..." }],
  "auditLogs": [{ "timestamp": "...", "action": "auth.login", "outcome": "success" }],
  "exportedOnUtc": "2024-12-01T10:00:00Z"
}
```

Hassas alanlar yok: PasswordHash, TotpSecret, fingerprint, refresh token hash — bunlar zaten kullanıcı bilgisi değil, internal credential.

---

## 4. Customer.Language

Kullanıcının kalıcı dil tercihi.

### Locale Resolution Önceliği

1. `?lang=tr` query parameter (tek seferlik override)
2. JWT `lang` claim (Customer.Language'den)
3. `Accept-Language` header
4. `App:DefaultLanguage` config

### Endpoint

```
GET  /api/v1/web/preferences/language          → { "language": "tr" }
PUT  /api/v1/web/preferences/language { "language": "tr" }
```

JWT'deki `lang` claim mevcut token'da değişmez — kullanıcı tekrar login olunca devreye girer. Backend email'leri vb. **yeni** action'lar yeni tercihi kullanır.

---

## 5. OpenAPI Codegen Pipeline

### Senaryo

Backend `CustomerDto` schema değişti, frontend hala eski tip kullanıyor → runtime hata.

### Çözüm

`tools/openapi-codegen/generate.mjs` Swagger JSON → TypeScript:
- `frontend/libs/shared/models/src/lib/generated.ts`
- `mobile/app/src/models/generated.ts`

CI workflow `.github/workflows/contract.yml`:
1. Backend'i background'da çalıştır
2. Generate çalıştır
3. Git diff kontrolü — değişiklik varsa PR fail

Developer akışı:
```bash
dotnet run --project src/SearchConsoleApp.Web/
# yeni terminal:
cd tools/openapi-codegen && node generate.mjs
git add frontend/.../generated.ts mobile/.../generated.ts
git commit
```

Tipler git'te — IDE autocomplete + compile-time kontrol + PR review'da görünür.

---

## 6. Inbox Pattern — Idempotent Webhook Receive

Outbox'ın simetrik karşıtı: **bize gelen** webhook'ları idempotent işle.

### Senaryo

Stripe webhook gönderiyor. Bizden 200 alamazsa retry yapar. Aynı `evt_xxx` ID'li event iki kez işlenmemeli (aynı sipariş iki kez teslim edilmiş işaretlenmesin).

### Tablo

```
InboxMessage
├─ Source              ("stripe", "github", "twilio")
├─ ExternalEventId     (provider'ın stable ID'si)
├─ EventType           ("charge.succeeded")
├─ Payload             (raw JSON, retry için)
├─ Status              ("received" | "processed" | "failed")
└─ UNIQUE(Source, ExternalEventId)   ← idempotency guarantee
```

### Kullanım

```csharp
public async Task<IActionResult> Receive(string source)
{
    var rawBody = await ReadBodyAsync();
    if (!VerifySignature(source, rawBody)) return Unauthorized();

    var eventId = Request.Headers["X-Webhook-Event-Id"];
    var (eventType, _) = ParseFromBody(rawBody);

    var result = await _inbox.TryRecordAsync(source, eventId, eventType, rawBody);

    if (result.AlreadyProcessed)
        return Ok(new { duplicate = true });   // idempotent — 200 dön

    try
    {
        await ProcessAsync(rawBody);
        await _inbox.MarkProcessedAsync(result.Id);
        return Ok();
    }
    catch (Exception ex)
    {
        await _inbox.MarkFailedAsync(result.Id, ex.Message);
        return StatusCode(500);   // provider retry yapar
    }
}
```

### Race Condition Koruması

İki istek aynı anda gelirse (provider'ın retry'i ilk istek bitmeden gelir):
- DB-level `UNIQUE(Source, ExternalEventId)` constraint
- `DbUpdateException` yakalanır → "AlreadyProcessed" döner
- Diğer worker'ın kaydını okuyup return — race güvenli

### Provider-specific Signature

Her provider farklı imzalama yapar — receiver controller `VerifySignature(source, ...)` ile dispatch eder:

| Provider | Header | Algo |
|---|---|---|
| Stripe | `Stripe-Signature` | HMAC-SHA256 + timestamp |
| GitHub | `X-Hub-Signature-256` | HMAC-SHA256 |
| Twilio | `X-Twilio-Signature` | HMAC-SHA1 + URL+params |

Şablon generic örnek içerir (`X-Webhook-Signature: sha256=...`). Üretimde provider'ın resmi SDK'sini kullan (`Stripe.NET`, `Octokit`).

### Config

```json
"Webhooks": {
  "Receive": {
    "stripe": { "SigningSecret": "whsec_xxx" },
    "github": { "SigningSecret": "..." }
  }
}
```

Secret yoksa → 401. Production'da env var:
```bash
Webhooks__Receive__stripe__SigningSecret=whsec_xxx
```

### Inbox vs Outbox

Sık karıştırılan ayrım:

|  | Outbox | Inbox |
|---|---|---|
| Yön | Biz → Dış | Dış → Biz |
| Amaç | Reliable delivery | Idempotent receive |
| Garanti | At-least-once | Exactly-once processing |
| Kaynak | Business transaction | External event |
| Retry | Bizim worker | Provider'ın işi |

İki tablo benzer schema ama amaçları zıt — birleştirmek yanıltıcı olur.

---

## 7. Distributed Tracing

OpenTelemetry zaten kurulu; v18'de Jaeger entegrasyonu + sampling eklendi.

### Backend Stack

```
HTTP request gelir
   ↓
[ASP.NET Core auto-instrumentation] → root span
   ↓
[HttpClient instrumentation] → outbox webhook çağrıları
   ↓
[Manual ActivitySource] → service-level custom spans
   ↓
OTLP exporter → Jaeger/Tempo/Honeycomb
```

### Manuel Span Yaratma

```csharp
public async Task RefundAsync(long orderId, decimal amount)
{
    using var activity = OpenTelemetrySetup.StartActivity("OrderService.Refund");
    activity?.SetTag("order.id", orderId);
    activity?.SetTag("refund.amount", amount);

    try
    {
        await ProcessRefundAsync(orderId, amount);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

### Sampling Stratejisi

```json
"Observability": {
  "SampleRate": 0.1   // %10 — production default
}
```

- **`1.0`**: Tüm trace'ler (dev only — pahalı)
- **`0.1`**: %10 (production önerisi)
- **Parent-based**: Caller trace'i zaten sample'lıysa biz de örnekleriz (tutarlılık)
- **Error trace'leri**: Sampling'den bağımsız her zaman kaydedilir

### Dev Stack

`docker-compose.yml` Jaeger içerir:

```bash
docker compose up -d
# Jaeger UI: http://localhost:16686
```

API otomatik olarak `http://jaeger:4317` OTLP endpoint'ine bağlanır. SampleRate dev'de `1.0` (her trace).

### Health Endpoint'leri Filtrelendi

`/health/*` istekleri trace edilmiyor — k8s liveness probe'ları her saniye atıyor, sample storage'ı doldurur.

### Cross-service Correlation

`traceparent` HTTP header'ı W3C standart formatında. HttpClient instrumentation otomatik propagate eder — service A → service B çağrısında trace ID aynı kalır.

```
[Frontend] → trace=abc123
            ↓ (traceparent: 00-abc123-...)
[Backend]  → trace=abc123, span=parent
            ↓ HttpClient → traceparent propagated
[Stripe webhook outbox] → trace=abc123, span=child
```

Jaeger UI'da tek bir distributed trace olarak görünür.

