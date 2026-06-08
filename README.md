# SearchConsoleApp Template

> nopCommerce'in olgun mimari desenlerinden esinlenen, .NET 9 + **Angular** tabanlı kurumsal uygulama şablonu. Backend pure JSON API, frontend üç ayrı SPA.

## 📦 İçindekiler

```
SearchConsoleApp-template/
├── docs/
│   ├── ARCHITECTURE.md          ← Mimari kuralları (oku!)
│   └── MULTI_TENANCY.md         ← Tenant reçetesi (opt-in)
├── prompts/
│   └── AI_PROMPT_TEMPLATE.md    ← Claude/Copilot için sistem prompt
├── src/                         ← .NET backend
│   ├── SearchConsoleApp.Core/              # Entity, interface, marker, event
│   ├── SearchConsoleApp.Data/              # EF Core, Repository, DbContext
│   ├── SearchConsoleApp.Services/          # İş mantığı
│   ├── SearchConsoleApp.Web.Framework/     # API base, JWT, filter
│   └── SearchConsoleApp.Web/               # API host (Controllers/Public, /Web, /Admin)
├── frontend/                    ← Nx monorepo (Angular 21)
│   ├── apps/
│   │   ├── public-app/          # Anonim (port 4200)
│   │   ├── web-app/             # Üye (port 4201)
│   │   └── admin-app/           # Admin (port 4202)
│   ├── libs/shared/             # Ortak kod (core, ui, theme, models, utils)
│   ├── themes/                  # Müşteri tema JSON'ları
│   └── package.json
├── mobile/                      ← Expo SDK 56 (opsiyonel)
│   └── app/                     # Tek app, role-based (public/web/admin)
├── tools/
│   └── openapi-codegen/         # Backend OpenAPI → TS types (web + mobile)
└── SearchConsoleApp.sln
```

## 🚀 Yeni Proje Başlatma

```bash
# 1. Şablonu kopyala, isim değiştir
grep -rl "SearchConsoleApp" . | xargs sed -i 's/SearchConsoleApp/YourApp/g'
find . -depth -name "*SearchConsoleApp*" -execdir bash -c 'mv "$1" "${1//SearchConsoleApp/YourApp}"' _ {} \;

# 2. Backend
dotnet build
dotnet run --project src/SearchConsoleApp.Web        # localhost:5000

# 3. Frontend (her biri ayrı terminal)
cd frontend
ng new public-app --routing --style=scss --standalone --skip-git
# (stub dosyaları yeni Angular projesine kopyala)
cd public-app && npm start                  # localhost:4200
```

## 🧠 Üç Şey Öğren

### 1) Bağımlılık yönü
```
Frontend (Angular) → HTTP → Web (API) → Web.Framework → Services → Data → Core
```

### 2) İki kimlik
- `Id` (long) → internal, FK, index
- `EntityId` (Guid v7) → public, URL, API response

API URL'lerinde **her zaman `EntityId`** kullan.

### 3) Üç audience
| Audience | Backend prefix | Frontend | Auth |
|---|---|---|---|
| Public | `/api/public/*` | `public-app` | Anonim |
| Web | `/api/web/*` | `web-app` | JWT (user) |
| Admin | `/api/admin/*` | `admin-app` | JWT (admin) |

Yeni endpoint yazarken bu üçten birine yerleştir.

## ✨ Önemli Özellikler

| Özellik | Nerede |
|---|---|
| **long + Guid çift kimlik** | `BaseEntity.cs` |
| **Soft delete global filter** | `SearchConsoleAppDbContext.cs` — `ISoftDeletable` |
| **EntityId unique index** | `SearchConsoleAppDbContext.cs` — otomatik |
| **Guid v7 auto-assign** | `EfRepository.InsertAsync` |
| **Otomatik event publish** | Repository |
| **Auto-DI marker** | `IScopedService` vb. |
| **Audience scope (kritik)** | `IRequestScope` + `ICacheKeyFactory` — cache/event/log/metric otomatik audience-aware |
| **Device + Session tracking** | Kalıcı cihaz kimliği + aktif oturum yönetimi (`docs/AUDIT.md`) |
| **2FA / MFA (TOTP)** | RFC 6238, authenticator app uyumlu, Device.Trusted ile bypass |
| **Email verify + Password reset** | SecurityToken altyapısı, IEmailSender (SMTP/log) |
| **Rate limiting** | Audience başına ayrı limit, auth endpoint brute force koruması |
| **Health checks** | `/health`, `/health/ready`, `/health/live` (k8s-ready) |
| **GeoIP (MaxMind)** | DeviceSession IP'den ülke/şehir otomatik doldurur |
| **Docker Compose** | API + SQL + Redis + Seq + Mailpit tek komutla (`docker compose up`) |
| **CI/CD** | GitHub Actions: build + test + Docker image push (GHCR) |
| **Integration tests** | WebApplicationFactory + InMemory DB + test email sender |
| **API versioning** | `/api/v1/...` URL segment, version-aware Swagger |
| **Blob storage** | `IBlobStorage`: local file + S3-uyumlu (AWS/MinIO/R2/Spaces) |
| **Permission-based auth** | Role → permission resolution, `[HasPermission(...)]` attribute |
| **SignalR real-time** | Session revoke broadcast + admin canlı audit feed, Redis backplane |
| **OAuth / Social login** | Google + Microsoft + GitHub, ExternalLogin entity, account linking |
| **Backend i18n** | JSON resource'lar, Accept-Language ile locale çözümleme |
| **AuditLog archive** | Batch'li gerçek archive impl, ayrı tablo (`AuditLogArchive`) |
| **Webhook outbox** | Transactional outbox, retry+backoff, dead-letter, HMAC imza (`docs/OUTBOX.md`) |
| **Feature flags** | OpenFeature-style, targeting + rollout %, maintenance mode |
| **GDPR compliance** | Right to be forgotten — anonymize + audit korunur, data export |
| **Customer.Language** | Kalıcı dil tercihi, JWT `lang` claim |
| **OpenAPI codegen CI** | Backend schema değişikliği PR'da diff olarak görünür |
| **Inbox pattern** | Idempotent webhook receive (Stripe, GitHub, vb.) |
| **Distributed tracing** | Jaeger entegrasyonu, sampling, custom span'ler (`docs/OUTBOX.md`) |
| **Backup strategy** | 3-2-1, PITR, DR drill, outbox/inbox restore (`docs/BACKUP.md`) |
| **Outbox retention** | Succeeded 7gün / dead 90gün otomatik cleanup |
| **AuditLog** | "Kim, ne zaman, nereden, ne yaptı" kalıcı DB kaydı, üç doldurma yolu |
| **Auto-DI (Scrutor)** | `ServiceCollectionExtensions.AddSearchConsoleAppServices()` — marker'ları otomatik tarar |
| **HybridCache (.NET 9)** | Memory veya Redis, config'e göre. Audience-scoped key'ler |
| **Event bus** | `EventPublisher` in-process, paralel + audience filter |
| **Serilog enrichment** | Her log audience/tenant/customer/correlation otomatik taşır |
| **OpenTelemetry** | Metrics + traces, audience tag'leriyle |
| **OpenAPI codegen** | `tools/openapi-codegen/` — backend kontratlarından TS otomatik üret |
| **DB seeding** | `DbSeeder` — default temalar + dev admin user |
| **Token cleanup job** | `RefreshTokenCleanupService` — 24h'de bir expired token'ları temizler |
| **Multi-tenancy (opt-in)** | `docs/MULTI_TENANCY.md` |
| **Mobile (opt-in)** | `docs/MOBILE.md` — Expo SDK 56, tek app + role-based, React Query, i18n |
| **JWT auth + 3 policy** | `JwtSetup.cs`, `AudienceControllers.cs` |
| **API response zarfı** | `ApiResponse<T>` — frontend unwrap eder |
| **Global exception filter** | `GlobalExceptionFilter.cs` — RFC 7807 |
| **Audience-aware Swagger** | 3 ayrı doc: public/web/admin |
| **CORS 3 origin** | `Program.cs` — appsettings |

## 🤖 AI ile Geliştirme

`prompts/AI_PROMPT_TEMPLATE.md`'i Claude/Copilot'a sistem mesajı olarak ver.

> **Sen:** "Order entity'si + admin için CRUD endpoint'leri yaz. Soft delete olsun."
>
> **AI çıktısı:**
> - Entity (`BaseEntity, ISoftDeletable`)
> - Mapping
> - IOrderService + impl (cache, virtual, IScopedService)
> - `Controllers/Admin/OrdersController.cs` (AdminApiController'dan türer)

## ❌ Bu Şablonda Olmayan

- ~~Plugin sistemi~~ (kaldırıldı)
- ~~int Id~~ (long + Guid)
- ~~Her entity'de Deleted zorunluluğu~~ (ISoftDeletable opt-in)
- ~~MVC View / Razor~~ (pure JSON API + Angular SPA'lar)
