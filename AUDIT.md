# Identity Tracking & Audit Log

Bu doküman üç birbiriyle ilişkili sistemi açıklar:

1. **Device** — Kullanıcının kalıcı cihaz kimliği
2. **DeviceSession** — Aktif login oturumu
3. **AuditLog** — "Kim, ne zaman, nereden, ne yaptı" kalıcı kayıt

## JWT session_id Claim

Login sonrası üretilen JWT'ye `sid` (session.Id) claim'i eklenir. Bu sayede:

- **`IsCurrent` çalışır** — Frontend "Oturumlarım" sayfasında "bu cihaz" işaretini gösterir
- **AuditLog `ActorSessionId` dolar** — her business action hangi session'dan geldi izlenebilir
- **"Revoke others" doğru çalışır** — mevcut session HARİÇ tutar

```
Login akışı:
1. parola doğrula
2. (2FA varsa) → ikinci adım
3. Device.GetOrCreate
4. RefreshToken oluştur
5. DeviceSession.StartAsync → session.Id alınır
6. JwtIssuer.IssueAccessToken(customer, session.Id) → JWT'ye sid=session.Id
```

JWT decode'da `IRequestScope.SessionId` ile erişilir.

## GeoIP Lookup (MaxMind GeoLite2)

DeviceSession kaydında `IpCountry` ve `IpCity` otomatik doldurulur.

**Setup:**
1. GeoLite2-City.mmdb dosyasını indir:
   https://dev.maxmind.com/geoip/geolite2-free-geolocation-data
2. `src/SearchConsoleApp.Web/App_Data/GeoLite2-City.mmdb` yoluna koy (veya `appsettings.json`'da `GeoIp:DatabasePath` ayarla)
3. Restart — `MaxMindGeoIpService` boot'ta DB'yi yükler

Dosya yoksa: graceful fallback. `IpCountry` ve `IpCity` null kalır, sistem normal çalışır. Log'da bir uyarı düşer.

**Loopback ve private IP'ler atlanır** — `192.168.*`, `10.*`, `127.0.0.1` için lookup yapılmaz.

## Retention & Cleanup

`AuditCleanupService` 24 saatte bir çalışır:

```json
"Audit": {
  "Retention": {
    "Mode": "delete",       // delete | archive
    "AuditLogDays": 730,     // 2 yıl
    "RevokedSessionDays": 90,
    "IntervalHours": 24
  }
}
```

- **AuditLog**: 2 yıldan eski kayıtlar silinir (KVKK/GDPR'ye uygun, sektörünüze göre ayarlayın)
- **DeviceSession**: revoke olalı 90 gün geçenler silinir (audit'leri zaten yakalandı)
- **archive mode**: placeholder — production'da AuditLogArchive tablosuna COPY job kur

## 2FA / MFA

TOTP (RFC 6238) — standart authenticator app'ler (Google Authenticator, Authy, 1Password) ile uyumlu.

### Setup akışı

```
1. POST /api/web/2fa/setup
   → { secret, otpAuthUri }
   → Kullanıcı authenticator app'e QR'ı tarar veya secret'ı manuel girer
   → DB'ye HENÜZ yazılmaz — frontend geçici tutar

2. POST /api/web/2fa/enable { secret, code }
   → İlk doğrulanan code 2FA'yı aktive eder
   → Backend Customer.TotpSecret kaydeder + 10 recovery code üretir
   → Recovery code'lar response'ta BİR KEZ döner — DB'de SHA-256 hash'i tutulur

3. Kullanıcı recovery code'ları güvenli yere kaydeder
```

### Login akışı (2FA aktif)

```
1. POST /api/public/auth/login { email, password }
   → 2FA aktif ve Device.Trusted = false → response: { requiresTwoFactor: true, preAuthToken: "..." }

2. POST /api/public/auth/login/2fa { preAuthToken, code, useRecoveryCode }
   → Code doğru → normal AuthTokens response
   → preAuthToken bir kez kullanılır (replay attack koruması)
```

### Device.Trusted ile 2FA bypass

```
if (customer.TwoFactorEnabled && !device.Trusted) → 2FA gerekli
if (device.Trusted) → 2FA atlanır
```

Kullanıcı `PATCH /api/web/devices/{id}/trust { trusted: true }` ile cihaza güvenebilir. Sonraki login'lerde bu cihazdan 2FA istenmez.

### Recovery code'ları

- 10 adet, format: `XXXXX-XXXXX` (50 bit entropy)
- Tek kullanım — kullanıldığında listeden silinir
- Tükenirse `POST /api/web/2fa/recovery-codes/regenerate` ile yeniden üretilir (eski hepsi geçersizleşir)

### Disable

```
POST /api/web/2fa/disable { password }
→ Parola doğrulanır (re-auth requirement)
→ TwoFactorEnabled=false, TotpSecret=null, RecoveryCodesHashes=null
```

---

## Email Verification & Password Reset

### Ortak altyapı: `SecurityToken`

Aynı tablo iki akış için (`Purpose` ile ayrılır):
- `"email_verification"` — 24 saat TTL
- `"password_reset"` — 1 saat TTL

Token DB'de **SHA-256 hash** olarak saklanır. Raw token sadece email link'inde gider.

**Tek aktif token kuralı:** Aynı customer + purpose için yeni token üretilince eskisi revoke edilir.

### Email Verification

```
Register → AuthService otomatik SendEmailVerificationAsync
        → IEmailSender → link: ${App:PublicUrl}/verify-email?token=...

Kullanıcı linke tıklar:
POST /api/public/auth/email/verify { token }
   → Customer.EmailConfirmed = true

Tekrar gönderme:
POST /api/public/auth/email/resend-verification { email }
   → Enumeration koruması: her durumda 200
```

### Password Reset

```
"Şifremi unuttum":
POST /api/public/auth/password/forgot { email }
   → Enumeration koruması: her durumda 200
   → Varsa: mail gönder

Reset:
POST /api/public/auth/password/reset { token, newPassword }
   → PasswordHash güncellenir
   → GÜVENLİK: TÜM aktif session'lar revoke edilir

Login halindeyken:
POST /api/web/account/password/change { currentPassword, newPassword }
```

### Email Provider

```json
"Email": {
  "Mode": "log" | "smtp",   // log = dev, smtp = prod
  "Smtp": { "Host": "...", "Port": 587, "Username": "...", ... }
}
```

Production-grade alternatifler (SendGrid, SES, Postmark): `IEmailSender`'ı swap et.

---

## Rate Limiting

| Policy | Partition | Default |
|---|---|---|
| `public-rate` | IP | 60/dakika |
| `web-rate` | CustomerId | 300/dakika |
| `admin-rate` | CustomerId | 600/dakika |
| `auth-rate` | IP+endpoint | 10/dakika |

Auth controller `[EnableRateLimiting("auth-rate")]` — brute force koruması.

Limit aşıldığında: **429** + `Retry-After` header.

Config:
```json
"RateLimit": {
  "Public": { "PermitLimit": 60, "WindowSeconds": 60 },
  ...
}
```

---

## Health Checks

```
GET /health         → tüm checks
GET /health/ready   → "ready" tag'li (DB, Redis) — readiness probe
GET /health/live    → liveness probe
```

Kontrol edilenler:
- **`db`** — SQL Server bağlantısı (Unhealthy)
- **`redis`** — sadece `Cache:Provider=redis` ise (Degraded)
- **`geoip`** — DB yüklü mü (Degraded)

Kubernetes:
```yaml
livenessProbe: { httpGet: { path: /health/live, port: 80 } }
readinessProbe: { httpGet: { path: /health/ready, port: 80 } }
```

---

## Permission-Based Authorization

Role'ler kullanıcıya verilir, permission'lar role'lerden **resolve** edilir:

```
Customer.Roles = "admin"
  ↓ (RolePermissions.ResolveForRoles)
Permissions = ["customers.read", "customers.write", "audit.read", ...]
  ↓ (JwtIssuer)
JWT claims: perm=customers.read, perm=customers.write, ...
```

### Permission tanımı

`SearchConsoleApp.Core/Auth/Permissions.cs` — sabit string'ler. Format: `{resource}.{action}`.

Örnek roller (`RolePermissions`):
- `user` → boş (Web audience giriş yeter)
- `admin` → tüm permission'lar
- `support-agent` → `customers.read`, `audit.read`, `sessions.read.any`
- `operator` → `sessions.read.any`, `sessions.revoke.any`, `system.health`

### Controller kullanımı

```csharp
[HasPermission(Permissions.AuditRead)]
public class AuditController : AdminApiController { }

// Action-level:
[HasPermission(Permissions.CustomersWrite)]
[HttpPost]
public async Task<IActionResult> Create(...) { }
```

Permission yoksa → 403 Forbidden.

---

## Real-time (SignalR)

Hub: `/hubs/notifications`. JWT auth (query string token).

### İki broadcast grubu

- **`user-{customerId}`** — o kullanıcıya yönelik
  - `SessionRevoked` — açık tab'ler hemen logout olur
  - `UserNotification` — toast/banner
- **`admin`** — tüm admin'lere
  - `AuditEvent` — canlı audit feed

### Service'lerden broadcast

`SessionService.RevokeAsync` ve `AuditService.LogAsync` **otomatik** broadcast yapar:

```csharp
// SessionService:
await _broadcaster.SessionRevokedAsync(customerId, sessionId, reason);

// AuditService:
await _broadcaster.AuditEventAsync(new AuditEventBroadcast(...));
```

Manuel:
```csharp
await _broadcaster.NotifyUserAsync(customerId, "Başlık", "Mesaj", "warning");
```

### Frontend (Angular)

```typescript
const notif = inject(NotificationService);
await notif.connect();   // login sonrası

notif.sessionRevoked$.subscribe(e => { /* auto-logout */ });
notif.auditEvent$.subscribe(e => { /* canlı feed */ });
```

### Config

```json
"Realtime": {
  "Enabled": true,
  "Backplane": { "Redis": "localhost:6379" }
}
```

Multi-instance: Redis backplane **zorunlu** — user A pod1'e bağlı, broadcast pod2'den geliyorsa mesaj kaybolur.

---

## OAuth / Social Login

Desteklenen: **Google**, **Microsoft**, **GitHub**.

### Akış (Authorization Code, BFF pattern)

```
1. Frontend → GET /api/v1/public/auth/external/google?returnUrl=/profile
   → { authorizeUrl } döner, frontend redirect eder

2. Provider'da user kabul → frontend callback URL'ine code ile döner

3. Frontend → POST /api/v1/public/auth/external/google/callback { code, state }
   → AuthTokens döner (normal JWT)
```

State CSRF koruması: 10dk in-memory cache.
Email match (verified) varsa → mevcut Customer'a otomatik bağlanır.
OAuth-only Customer: `PasswordHash = null` (parola login kapalı, sadece provider üzerinden).

### Link/Unlink (login halindeyken)

```
POST   /api/v1/web/account/external/{provider}/link
DELETE /api/v1/web/account/external/{provider}
GET    /api/v1/web/account/external
```

**Unlink koruması**: Son giriş yolu (parola yok + tek provider) → 400 hata.

### Yeni provider ekleme

1. `ExternalAuthService.GetProviderConfig` switch'ine case
2. `ParseUserInfo` switch'ine parser
3. appsettings'e config:
   ```json
   "OAuth": {
     "google": { "ClientId": "...", "ClientSecret": "...", "RedirectUri": "..." }
   }
   ```

Provider'larda Authorized Redirect URI eşleşmeli.

---

## AuditLog Archive

`AuditCleanupService` `Audit:Retention:Mode = "archive"` ile:

1. **Batch'li** (1000 kayıt) `AuditLog` → `AuditLogArchive` tablosuna kopyala
2. Aynı batch aktif tablodan sil
3. Batch'ler arası 100ms delay
4. Loop bitene kadar

`AuditLogArchive` aynı schema, ayrı tablo. Hot/cold data ayrımı.

Production önerisi: archive tablosunu ayrı DB'ye veya read-replica'ya taşı — kod aynı kalır.

---

## Backend i18n

`Resources/messages.{en,tr}.json` — JSON key-value.

```csharp
public class MyService(ILocalizationService localizer)
{
    public void Throw() => throw new UnauthorizedAccessException(
        _localizer.Get("auth.invalid_credentials"));
}
```

Locale çözümleme: `Accept-Language` header → fallback `App:DefaultLanguage`.

Parametre interpolasyonu:
```json
{ "validation.required": "{0} is required." }
```
```csharp
_localizer.Get("validation.required", "Email")  // → "Email is required."
```

Yeni dil: `Resources/messages.{lang}.json` ekle + `App:SupportedLanguages` config.

---

## PreAuthTokenStore (Multi-instance)

2FA flow ilk adım/ikinci adım arası geçici token.

Config-driven impl seçimi (`AuthSetup.AddSearchConsoleAppPreAuthStore`):

- `Cache:Provider = "memory"` → `InMemoryPreAuthTokenStore` (single-instance/dev)
- `Cache:Provider = "redis"` → `DistributedPreAuthTokenStore` (multi-instance)

Multi-instance + memory: pod1'de oluşan token pod2'de consume edilemez → 2FA login kırılır. **Production'da Redis zorunlu.**

---

## Üç Sistem Birarada

```
┌─────────────┐         ┌───────────────┐          ┌──────────────┐
│  Customer   │───1:N──▶│    Device     │──1:N────▶│DeviceSession │
└─────────────┘         └───────────────┘          └──────────────┘
       │                                                    │
       │                                                    │
       └────────────────┬───────────────────────────────────┘
                        │
                        ▼
                ┌──────────────┐
                │   AuditLog   │  (her business action burada toplanır)
                └──────────────┘
```

## 1. Device — Kalıcı Cihaz Kimliği

**Ne yapar?** Aynı kullanıcının "iPhone" ile "iş laptop"u ayrı kayıt tutar. Her cihaz fingerprint ile tanınır.

**Fingerprint:** SHA-256 hex hash
- Inputs: `UserAgent` + `Accept-Language` + `Platform` + `X-Device-Fingerprint` header
- Mobile için: Expo `Constants.installationId` `X-Device-Fingerprint` header'ı olarak gönder

**Akış:**
```
Login → AuthService → IDeviceService.GetOrCreateAsync(customerId, fingerprintInput)
       → Aynı fingerprint var: LastSeenUtc güncellenir
       → Yoksa: yeni Device kaydı oluşur
```

**Kullanıcı görünür özellikleri:**
- `Name`: "Ali'nin iPhone" — kullanıcı atayabilir
- `Trusted`: 2FA atlanabilir flag'i
- `DeviceType`: web | mobile-ios | mobile-android | desktop

**Endpoint'ler:**
- `GET /api/web/devices` — kendi cihazları
- `PATCH /api/web/devices/{entityId}/rename` — isim ata
- `PATCH /api/web/devices/{entityId}/trust` — güven flag'i

## 2. DeviceSession — Aktif Oturum

**Ne yapar?** Bir Device'tan açılmış login oturumu. **RefreshToken ile 1:1**.

```
DeviceSession {
  DeviceId          → hangi cihazdan
  CustomerId        → kim
  Audience          → 'web' | 'admin' (nereye girdi)
  IpAddress, IpCountry, IpCity  → nerden
  UserAgent         → ne ile
  StartedUtc        → ne zaman başladı
  LastActivityUtc   → en son ne zaman aktifti
  RevokedUtc        → kapatıldı mı?
  RevokedReason     → user | admin | expired | security | rotation
  RefreshTokenHash  → RefreshToken ile bağlantı
}
```

**Otomatik akış:**
```
Login → Session başlar (StartAsync)
Her refresh → Session.LastActivityUtc güncellenir (UpdateActivityAsync)
Refresh rotation → eski Session revoked='rotation', yeni Session başlar
Logout → Session revoked='user'
RevokeAll (panic) → tüm session'lar revoked='security'
```

**Kullanıcı endpoint'leri:**
- `GET /api/web/sessions` — aktif oturumlar
- `GET /api/web/sessions/history` — geçmiş
- `POST /api/web/sessions/{id}/revoke` — belirli oturumu kapat
- `POST /api/web/sessions/revoke-others` — diğer cihazlardan çık

**Admin endpoint'leri:**
- `GET /api/admin/customers/{id}/sessions` — kullanıcının aktif oturumları
- `POST /api/admin/sessions/{id}/revoke` — admin zorla kapatır
- `POST /api/admin/customers/{id}/sessions/revoke-all` — hesap askıya al

## 3. AuditLog — Business Audit Trail

**Ne yapar?** "Kim, ne zaman, nereden, ne yaptı" sorusunu yıllarca cevaplar.

### Serilog'tan Farkı

| | Serilog | AuditLog |
|---|---|---|
| Amaç | Operational (debug, perf, error) | Business event'leri |
| Saklama | 30 gün (genelde) | Yıllarca / kalıcı |
| Sorgulanabilir | Log explorer'da | DB'de SQL ile |
| Hukuki kayıt | Hayır | Evet (GDPR/KVKK) |
| Volume | Yüksek | Düşük-orta |

İkisi paralel çalışır — `CorrelationId` ile cross-ref yapılır.

### Üç Doldurma Yolu

**A) Otomatik — tüm entity değişimleri**

EfRepository `IEntityChangeNotifier`'a haber verir. `AuditableEntityNotifier` AuditLog'a yazar:

```
Customer update yapıldı
→ EfRepository.UpdateAsync
   → EF ChangeTracker'dan before/after çekilir
   → SaveChangesAsync
   → EventPublisher.PublishAsync(EntityUpdatedEvent)
   → IEntityChangeNotifier.NotifyAsync(Updated, customer, changes)
      → AuditService.LogAsync({
           Action: "customer.update",
           TargetType: "Customer",
           TargetId: 42,
           ChangesJson: {"Email": {"old": "x@y.com", "new": "z@y.com"}}
        })
```

**Tüm entity'ler için otomatik** — manuel kod yazmaya gerek yok.

Excluded types (yüksek volume / self-reference):
- `AuditLog` (sonsuz döngü)
- `DeviceSession` (kendi tablosu zaten audit)

Sensitive fields (maskelenir):
- `PasswordHash`, `TokenHash`, `RefreshTokenHash`, `Fingerprint`, `PrivateKey`, `Secret`
- ChangesJson'da `{"PasswordHash": {"old": "***", "new": "***"}}` — "değişti" bilgisi var ama değer yok

**B) Attribute — controller action'larında semantic isim**

```csharp
[HttpPost("login")]
[Audit("auth.login")]   // ← semantik isim
public async Task<IActionResult> Login(...) { ... }

[HttpPost("{id}/reset-password")]
[Audit("customer.password_reset", TargetType = "Customer", TargetIdRouteKey = "id")]
public async Task<IActionResult> ResetPassword(long id) { ... }
```

`AuditFilter` global olarak kayıtlı — `[Audit]` taşıyan action'lar otomatik yakalanır.

**C) Manuel — service içinden**

```csharp
public class OrderService
{
    private readonly IAuditService _audit;

    public async Task RefundAsync(Order order, decimal amount, string reason)
    {
        // ... business logic ...

        await _audit.LogAsync(new AuditEntry
        {
            Action = "order.refund",
            TargetType = "Order",
            TargetId = order.Id,
            MetadataJson = $"{{\"amount\": {amount}, \"reason\": \"{reason}\"}}",
        });
    }
}
```

### Otomatik Doldurulan Alanlar

`IAuditService.LogAsync` çağrılınca **caller'ın vermediği** alanlar otomatik:
- `Timestamp` — şu an
- `Audience` — IRequestScope'tan (public/web/admin/bg)
- `ActorCustomerId` — IRequestScope'tan
- `ActorEmail` — Customer lookup
- `ActorIp` — HttpContext.Connection.RemoteIpAddress
- `ActorUserAgent` — Request.Headers.UserAgent
- `CorrelationId` — HttpContext.TraceIdentifier
- `TenantId` — IRequestScope'tan

### Sorgu Endpoint'leri (Admin)

- `GET /api/admin/audit` — filter'lı liste
  - Query: `?actorCustomerId=42&from=2024-01-01&take=100`
- `GET /api/admin/audit/customers/{id}` — kullanıcının son aktivitesi
- `GET /api/admin/audit/entity/Customer/42` — bu entity'ye yapılan her şey

## Change Tracking — Açma/Kapama

EF ChangeTracker before/after captures **default açık**. Kapatmak için:

```json
{
  "Audit": {
    "CaptureChanges": false
  }
}
```

Ne zaman kapatılır?
- Production yüksek throughput'ta (her update'te `Entry.Properties` iteration az da olsa cost)
- ChangesJson kullanılmıyorsa (sadece "kim, ne" yeter, "ne değişti" gerek yok)

## Storage Stratejisi

**Aynı DB, ayrı tablo** (default):
- Avantaj: transactional bütünlük, JOIN sorguları, tek backup
- Veri büyüdükçe: tablo partition (Timestamp'e göre aylık partition)

**Alternatif: stream-out** (yüksek volume):
```json
{
  "Audit": {
    "Storage": "db+stream"   // veya "stream-only"
  }
}
```

Stream-out: aynı AuditLog kaydı Serilog'a paralel akar → Elastic/Datadog/CloudWatch indeksler. DB sorgu için kalır, indeks dış sistemde.

## Performance Notları

**AuditLog volume tahmini:**
- 1000 günlük aktif kullanıcı × ortalama 50 entity değişikliği/gün = 50k satır/gün
- 1 yılda ~18M satır — index'lerle hızlı sorgulanabilir

**Recommended indexes (zaten kuruldu):**
- `(Timestamp)` — tarih aralığı
- `(ActorCustomerId)` — kullanıcı geçmişi
- `(TargetType, TargetId)` — entity geçmişi
- `(Action)` — action filtreleme
- `(CorrelationId)` — Serilog cross-ref

**Cleanup job (ileride eklenebilir):**
- 2 yıllık retention politikası → eski kayıtları arşive taşı
- Şu an cleanup yok — tablo büyüyebilir, planla

## Frontend Görüntüleme

**Mobile/Web "Oturumlarım" ekranı:**
```typescript
const sessions = await apiClient.get<SessionDto[]>('sessions', { audience: 'web' });

sessions.map(s => (
  <View>
    <Text>{s.userAgent}</Text>
    <Text>{s.ipAddress} ({s.ipCountry})</Text>
    <Text>{s.lastActivityUtc}</Text>
    {!s.isCurrent && <Button onClick={() => revoke(s.id)}>Bu oturumu kapat</Button>}
  </View>
))
```

**Admin audit timeline:**
```typescript
const logs = await apiClient.get<AuditLogDto[]>('audit', {
  audience: 'admin',
  params: { actorCustomerId: 42, take: 50 }
});

logs.map(l => (
  <Row>
    <Time>{l.timestamp}</Time>
    <Actor>{l.actorEmail} ({l.actorIp})</Actor>
    <Action>{l.action}</Action>
    <Target>{l.targetType}/{l.targetId}</Target>
    {l.changesJson && <DiffView json={l.changesJson} />}
  </Row>
))
```

## Yasaklar

❌ **PasswordHash audit'e ham yazılmaz** — `AuditSensitiveFields` filter zorla "***" yapar
❌ **AuditService.LogAsync'i try/catch'siz çağırma** — exception business action'ı bozabilir; AuditService kendi içinde catch'ler ama bilinçli ol
❌ **Audit kayıtlarını UI'dan düzenleme/silme** — kalıcı kayıt, hukuki gereklilik
❌ **DeviceSession.RefreshTokenHash'i log'a ham yazma** — sensitive
❌ **Audit volume'unu görmezden gelme** — production'a çıkmadan önce 1 ay'lık tahmini kayıt sayısını hesapla
