# SearchConsoleApp Mimari Kuralları

> Bu doküman, yeni .NET uygulamalarını **aynı yapıda, aynı kurallarla** geliştirmek için zorunlu kuralları tanımlar. nopCommerce'in olgun mimari desenlerinden esinlenir, ancak nopCommerce'e bağımlı değildir.

---

## 1. Temel Prensipler

1. **Katmanlı mimari** — Presentation → Services → Data → Core sırasıyla aşağı bağımlılık. Yukarı bağımlılık YASAK.
2. **Convention over configuration** — İsimlendirme ve klasör yapısı sıkı kuraldır.
3. **Async/await her yerde** — Senkron DB/IO yasak. `Async` suffix zorunlu.
4. **Auto-DI** — Marker interface'ler (`ISingletonService`, `IScopedService`, `ITransientService`).
5. **Event-driven** — `IEventPublisher` + `IConsumer<TEvent>`.
6. **Entity = POCO** — Domain entity'leri veri taşır, iş kuralları service'te.
7. **İki kimlik** — Internal `long Id` (FK, index) + public `Guid EntityId` (URL, API).
8. **Soft delete = opt-in** — Entity `ISoftDeletable` implement ederse global query filter otomatik.

---

## 2. Solution Yapısı

```
SearchConsoleApp.sln
└── src/
    ├── SearchConsoleApp.Core/            # Entity, interface, enum, abstraction
    ├── SearchConsoleApp.Data/            # EF Core DbContext, repository, mapping
    ├── SearchConsoleApp.Services/        # İş mantığı
    ├── SearchConsoleApp.Web.Framework/   # Filter, attribute, model binder
    └── SearchConsoleApp.Web/             # ASP.NET Core host
```

Bağımlılık yönü tek yönlü: **Web → Web.Framework → Services → Data → Core**

---

## 3. SearchConsoleApp.Core — Çekirdek

### BaseEntity
```csharp
public abstract class BaseEntity
{
    public long Id { get; set; }         // Internal — FK, index
    public Guid EntityId { get; set; }   // Public — URL, API
}

public interface ISoftDeletable
{
    bool Deleted { get; set; }
}
```

### Entity Kuralları
- Her entity `BaseEntity`'den türer.
- Sadece **property** — method/validation yok.
- Soft delete istiyorsan `ISoftDeletable` implement et.
- Public API'de/URL'de **`Id` değil `EntityId`** kullan.

---

## 4. SearchConsoleApp.Data — Veri Erişimi

```csharp
public interface IRepository<TEntity> where TEntity : BaseEntity
{
    Task<TEntity?> GetByIdAsync(long id);
    Task<TEntity?> GetByEntityIdAsync(Guid entityId);
    Task<IList<TEntity>> GetAllAsync(Func<IQueryable<TEntity>, IQueryable<TEntity>>? func = null);
    Task InsertAsync(TEntity entity, bool publishEvent = true);
    Task UpdateAsync(TEntity entity, bool publishEvent = true);
    Task DeleteAsync(TEntity entity, bool publishEvent = true);     // ISoftDeletable → soft
    Task HardDeleteAsync(TEntity entity, bool publishEvent = true); // her zaman fiziksel
    IQueryable<TEntity> Table { get; }
}
```

### Kurallar
- Repository **tek bir generic** — entity başına repo YOK.
- Karmaşık sorgular service katmanında `_repo.Table` üzerinden LINQ.
- Insert sırasında `EntityId` boşsa **Guid v7** (sıralı) otomatik atanır.
- Mapping **Fluent API** ayrı dosya — Data Annotation YASAK.
- **Global query filter:** `ISoftDeletable`'a `Deleted=false` otomatik. Silinmişleri görmek için `Table.IgnoreQueryFilters()`.
- **Otomatik unique index:** Her `BaseEntity.EntityId` için DbContext otomatik unique index ekler.
- Insert/Update/Delete sonrası otomatik event publish.

---

## 5. SearchConsoleApp.Services — İş Mantığı

### Kurallar
- Bir service = bir bounded context. `ICustomerService` + `CustomerService`.
- **`Async` suffix** zorunlu, **`virtual`** zorunlu.
- Constructor injection, field injection YASAK.
- `IScopedService` marker (auto-DI).
- Cache: `IStaticCacheManager` + `CacheKey` — manuel `MemoryCache` YASAK.
- Repo otomatik event yayar; özel domain event'leri (`OrderPlacedEvent`) service elle publish eder.
- Internal lookup → `GetByIdAsync(long)`. External lookup → `GetByEntityIdAsync(Guid)`.

---

## 6. Dependency Injection

### Marker interface'ler
- `IScopedService` — request başına (varsayılan)
- `ISingletonService` — uygulama ömrü
- `ITransientService` — her çağrıda yeni

### Auto-DI Mekanizması

**Scrutor** ile assembly scan. `Program.cs`'te:

```csharp
builder.Services.AddSearchConsoleAppServices(
    typeof(CustomerService).Assembly,
    typeof(JwtIssuer).Assembly
);
```

Bu tek satır şunları yapar:
1. `IDependencyRegistrar` implementasyonlarını `Order`'a göre çalıştırır
2. `IScopedService`/`ISingletonService`/`ITransientService` marker'lı tüm class'ları **AsImplementedInterfaces** ile kaydeder
3. `IConsumer<T>` implementasyonlarını event bus için kaydeder

Yeni service yazdığında DI kaydı için **ek bir şey yapmana gerek yok** — marker'ı implement etmek yeterli.

### Open Generic Repository

```csharp
services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
```

Bu Scrutor scan'inden ÖNCE yapılır.

---

## 6.1 Audience-Scoped Cross-Cutting Concerns

Sistem **üç audience**'a hizmet eder: `Public`, `Web`, `Admin`. Bu audience cache, event, log, metric gibi tüm cross-cutting concern'lere otomatik dahil olur.

### IRequestScope

Her isteğin ambient context'i. Service'ler DI ile alır:

```csharp
public interface IRequestScope
{
    Audience Audience { get; }
    long? TenantId { get; }
    long? CustomerId { get; }
    Guid? CustomerEntityId { get; }
    string? CorrelationId { get; }
}
```

HTTP context'inden otomatik resolve edilir (route prefix'e bakarak). Background job/scheduled task için `IRequestScopeMutator.BeginScope()` ile manuel set edilir.

### Cache — ICacheKeyFactory

Geliştirici elle prefix yazmaz, factory üretir:

```csharp
var key = _cacheKeys.For<Customer>("byid", customerId);
// → "SearchConsoleApp.web.customer.byid.42"      (audience=Web)
// → "SearchConsoleApp.admin.customer.byid.42"    (audience=Admin)
```

Aynı service method'u farklı audience'lardan çağrılınca **farklı cache key**'i üretir. Bu kritik bir güvenlik özelliği: admin'in zengin projeksiyonu web cache'ine sızamaz.

Multi-tenant'ta key'e tenant da eklenir: `SearchConsoleApp.admin.tenant5.customer.byid.42`.

### Event — Audience Filtering

`IAudienceAware` event'ler `SourceAudience` taşır (EventPublisher otomatik set eder). Consumer `IConsumerAudienceFilter` implement ederek seçici dinler:

```csharp
public class AdminAuditConsumer : IConsumer<EntityUpdatedEvent<Customer>>, IConsumerAudienceFilter
{
    public IReadOnlySet<Audience>? AllowedAudiences => new HashSet<Audience> { Audience.Admin };
    // Sadece admin'in yaptığı update'lerde tetiklenir
}
```

### Cache Invalidation — Cross-Audience

Bir Customer güncellendiğinde **tüm audience'lar** için invalidate edilir (admin'in update'i web'in cache'ini de geçersiz kılar):

```csharp
foreach (var prefix in _keys.AllAudiencePrefixesFor<Customer>())
    await _cache.RemoveByPrefixAsync(prefix);
```

### Logging — Serilog Enrichment

Her log event'ine otomatik `Audience`, `TenantId`, `CustomerId`, `CorrelationId` field'ları eklenir (`RequestScopeEnricher`). Structured logging stack'i (Elastic, Datadog, Seq) bu field'larla filtreleme yapabilir:

```
audience:admin AND level:error
```

### Metrics — OpenTelemetry Tags

Her metric ve trace'e `audience` tag'i otomatik eklenir (`AudienceTagEnricher`). Dashboard'larda audience-specific paneller mümkün:

```
http.server.duration{audience="admin"}
```

---

## 7. Event Sistemi

```csharp
public interface IConsumer<T> { Task HandleEventAsync(T eventMessage); }
public interface IEventPublisher { Task PublishAsync<T>(T eventMessage); }
```

### Implementation: EventPublisher

`SearchConsoleApp.Services.Events.EventPublisher` — in-process pub/sub. Verilen event tipi için tüm `IConsumer<T>` implementasyonlarını DI'dan resolve eder, **paralel** çalıştırır. Bir consumer hata fırlatırsa diğerleri etkilenmez (log'lanır).

Out-of-process event'e geçmek istersen (RabbitMQ, Kafka, Azure Service Bus): impl'i MassTransit veya Wolverine ile değiştir — interface aynı, service kodu değişmez.

### Otomatik event'ler
- `EntityInsertedEvent<T>`, `EntityUpdatedEvent<T>`, `EntityDeletedEvent<T>` — repository yayar.

### Domain event'leri
- `CustomerRegisteredEvent`, `OrderPlacedEvent` — service'de elle publish.

### Consumer registration
`IConsumer<TEvent>` implement eden her sınıf **otomatik** DI'a kaydolur (Scrutor). Örnek: `CustomerCacheInvalidator` — `Customer` event'lerini dinleyip cache prefix'ini temizler.

---

## 8. Caching

```csharp
public class CacheKey
{
    public string Key { get; }
    public TimeSpan? CacheTime { get; }
    public string[] Prefixes { get; }
}
```

### Implementation: HybridCache (.NET 9)

**Memory + Redis L1/L2.** `appsettings.json`:

```json
{
  "Cache": {
    "Provider": "memory",   // dev: tek instance, in-memory
    "Redis": {              // prod: multi-instance + Redis L2
      "ConnectionString": "localhost:6379"
    }
  }
}
```

Provider değişimi sadece config: kod değişmez. `IStaticCacheManager` interface'i her iki durumda da aynı.

### Kurallar
- Her cache key'in `Prefixes`'i dolu olur (HybridCache tag'leri).
- State değişimi sonrası prefix-based invalidation otomatik (`*CacheInvalidator` consumer'ları).
- Manuel `string.Format` ile key YASAK — `CacheKey.Create(args)` kullan.

---

## 9. Web Katmanı — Pure API + Üç Angular SPA

Backend **MVC/Razor yok, sadece JSON API**'dir. Üç ayrı Angular SPA tüketir.

### Backend Audience Yapısı

`SearchConsoleApp.Web` tek API host'tur; controller'lar üç namespace altında ayrılır:

| Namespace | Base Route | Audience | Auth |
|---|---|---|---|
| `Controllers/Public/` | `/api/public/*` | Anonim ziyaretçi | `[AllowAnonymous]` |
| `Controllers/Web/` | `/api/web/*` | Giriş yapmış üye | `Authorize(Policy=WebUser)` |
| `Controllers/Admin/` | `/api/admin/*` | Sistem admin'i | `Authorize(Policy=Admin)` |

Her controller bir base class'tan türer (`PublicApiController`, `WebApiController`, `AdminApiController`) ve route prefix ile policy otomatik gelir.

### JWT Auth

- Login → `POST /api/public/auth/login` → `{ token, expiresAt }` döner.
- Token claim'leri: `sub` (EntityId Guid), `uid` (long Id), `email`, `role`.
- `JwtTokenService` token üretir, `AddSearchConsoleAppJwtAuth` extension auth kurulumunu yapar.
- ExpiresMinutes, Issuer, Audience, Key → `appsettings.json` → `Jwt:*`.

### API Response Zarfı

Tüm başarılı yanıtlar `ApiResponse<T>` ile zarflanır:
```json
{ "success": true, "data": { ... }, "message": null }
```

Hatalar RFC 7807 ProblemDetails (`GlobalExceptionFilter` üretir):
```json
{ "status": 404, "title": "Not found", "detail": "...", "instance": "/api/..." }
```

### URL'lerde Guid
```
/api/admin/customers/{entityId:guid}    ✅ Public-safe, enumeration koruması
/api/admin/customers/{id:long}          ❌ Internal ID sızdırma
```

### CORS
Üç Angular port'u için açık: `4200` (public), `4201` (web), `4202` (admin). Production'da gerçek domain'lerle değiştirilir.

### Swagger
Audience başına ayrı dokümantasyon:
- `/swagger/public/swagger.json`
- `/swagger/web/swagger.json`
- `/swagger/admin/swagger.json`

### Frontend (Angular 21 + Nx Monorepo)

Tek workspace, üç app, **shared lib'ler**. Kod tekrarı yok.

**Workspace yapısı:**
```
frontend/
├── apps/
│   ├── public-app/     (port 4200)
│   ├── web-app/        (port 4201)
│   └── admin-app/      (port 4202)
├── libs/shared/
│   ├── core/           # ApiClient, AuthService, guard, interceptor
│   ├── ui/             # Ortak komponent (Button, ThemeSwitcher)
│   ├── theme/          # Theme service, loader, JSON-based temalar
│   ├── models/         # DTO interface'leri
│   └── utils/          # Helper
└── themes/             # Müşteri tema JSON'ları
```

**Teknoloji:**
- Angular 21.2 (en güncel kararlı)
- Standalone components, signal-based state
- Zoneless change detection (`provideZonelessChangeDetection`)
- OnPush her komponentte
- Nx 21 monorepo
- TypeScript 5.9, Vitest

**App config pattern:**
Her app `app.config.ts`'inde `provideSharedCore(config)` + `provideTheme(opts)` çağırır. Shared lib'ler `APP_CONFIG` injection token'ı üzerinden ayarları okur — her app farklı `apiBaseUrl`, `tokenStorageKey`, `requiredRole` ile çalışır ama kod tek yerde.

**Theme sistemi (multi-tenant):**
- CSS custom property tabanlı — runtime'da `:root`'a yazılır.
- `Theme` JSON dosyası: `themes/<name>.json`.
- Built-in: `default-light`, `default-dark`. Müşteri başına ek temalar.
- `ThemeSwitcher` komponenti kullanıcıya seçim verir.
- Backend'den de tema gelebilir (`/api/public/themes/<slug>`) — multi-tenant uygulamada her tenant'a özel renk.
- Komponentler renk sabitlemek YASAK; her zaman `var(--color-*)`.

Tam detay: `frontend/README.md`.

---

## 9.1 Mobile (Opt-In, varsayılan KAPALI)

Mobile uygulama gerekirse `mobile/app/` altında:
- **Expo SDK 56** (React Native 0.85, React 19.2) — bare CLI değil, modern Expo
- **TEK app**, role-based navigator: public/web/admin tek bundle, login durumuna göre stack seçilir
- Provider tabanlı DI (React Context), singleton YOK → test edilebilir
- React Query (server state), react-hook-form + zod (forms), react-i18next (i18n)
- Backend ile aynı `/api/public`, `/api/web`, `/api/admin` audience yapısı
- Tema sistemi web ile şema uyumlu — aynı JSON dosyaları (`frontend/themes/`)

Tam reçete: `docs/MOBILE.md`.

---

## 10. Auth Sistemi

JWT + Refresh Token paterni. Detaylar:

- **Login flow:** `POST /api/public/auth/login` → `{ accessToken, refreshToken, user }`
- **Register flow:** `POST /api/public/auth/register` → otomatik login + token döner
- **Refresh flow:** `POST /api/public/auth/refresh` → eski refresh revoke, yeni access + refresh
- **Logout:** `POST /api/public/auth/logout` → refresh token revoke
- **Password hash:** PBKDF2-SHA256, 100k iterations, format `iterations.salt.hash`
- **Refresh token:** SHA-256 hash DB'de, raw token client'ta. 64 byte random, URL-safe base64
- **Rotation:** Her refresh kullanımı eskiyi revoke eder, yeni üretir (replay attack koruması)
- **Token lifetimes:** Access 60dk, Refresh 30 gün (configurable)

`AuthController` (public) + `IAuthService` (services) + `IJwtIssuer` (core/auth) + `IPasswordHasher` (services/security).

---

## 11. İsimlendirme

| Tip | Örnek |
|---|---|
| Entity | `Customer` (tekil, PascalCase) |
| Interface | `ICustomerService` |
| Model | `CustomerModel`, `CustomerListModel` |
| Async method | `GetCustomerByIdAsync` |
| Event | `CustomerRegisteredEvent` (past tense) |
| Cache key | `SearchConsoleApp.customer.byid.{0}` (lowercase, dot-separated) |
| DB tablosu | `Customer` (tekil, entity ismiyle aynı) |

---

## 12. Multi-Tenancy (Opt-In, varsayılan KAPALI)

Şablon varsayılan olarak **tek tenant**'tır. Multi-tenant gerekirse:

- **Senaryo A (shared DB, satır seviyesi):** Entity'ye `ITenantScoped` ekle, DbContext global query filter otomatik filtreler. `ISoftDeletable` paterni ile aynı mantık.
- **Senaryo B (DB-per-tenant):** Tenant başına ayrı veritabanı, `ISearchConsoleAppDbContextFactory` ile dinamik context.

Hangi entity tenant'a ait, hangisi global (`Country`, `Currency`) — bu karar bilinçli verilir.

**Tam reçete:** `docs/MULTI_TENANCY.md` — kod parçaları, geçiş adımları, yaygın tuzaklar.

**Genel kural:** Multi-tenancy açıldığında **cache key'lerine tenant'ı dahil etmek zorunludur**, aksi halde tenant'lar arası veri sızıntısı olur.

---

## 13. Yasaklar

❌ Static service çağrıları
❌ Senkron DB çağrısı (`.Result`, `.Wait()`)
❌ **Razor View / MVC View dönüşü** — backend pure JSON API, View YOK
❌ **Controller'da iş mantığı** — sadece service çağrısı + ApiResponse
❌ **Audience controller'larını karıştırmak** — bir Web action'ı `/api/admin/*`'a router edilmez
❌ Service'te `HttpContext`'e doğrudan erişim
❌ Manuel `new ServiceX()` — DI kullan
❌ Cross-layer using (`Web` → `Data` doğrudan)
❌ `string.Format` ile cache key
❌ `try/catch` ile hata yutmak (GlobalExceptionFilter var)
❌ Public URL/API response'unda internal `long Id` (EntityId kullan)
❌ JWT token'ı URL query string'de göndermek
❌ Soft-deleted kayıt üzerinde update — önce `IgnoreQueryFilters()` ile çek
❌ CORS'u `AllowAnyOrigin` ile açmak — sadece whitelist origin
