# Multi-Tenancy Reçetesi (Opt-In)

> Bu dokümandaki kod **şablonda hazır değildir.** Multi-tenant gerektiğinde bu adımları izle. `ISoftDeletable` ile aynı paterni izler: opt-in, global query filter, sıfır overhead.

---

## Karar Ağacı

```
Tenant gerekiyor mu?
│
├─ Hayır  → Bu dosyayı kapat, hiçbir şey yapma. ✓
│
└─ Evet → Kaç tenant ve nasıl izolasyon?
   │
   ├─ Birkaç tenant, satır seviyesi izolasyon yeter
   │  → Senaryo A: Shared DB + Row-level filter
   │
   ├─ Tam izolasyon, tenant başına ayrı DB
   │  → Senaryo B: DB-per-tenant + Factory
   │
   └─ Hibrit (büyük müşteri ayrı DB, küçükler shared)
      → Senaryo A + B birlikte (gelişmiş)
```

**Çoğu durumda Senaryo A yeterli.** Senaryo B'yi sadece compliance (KVKK/GDPR ayrımı, müşteri talebi) veya 100GB+ tenant'lar için tercih et.

---

## Senaryo A — Shared DB + Row-Level Filter

### Adım 1: Core'a tenant abstraction'larını ekle

**`src/SearchConsoleApp.Core/MultiTenancy/ITenantScoped.cs`**
```csharp
namespace SearchConsoleApp.Core.MultiTenancy;

/// <summary>
/// Tenant'a ait entity'ler bu interface'i implement eder.
/// DbContext global query filter aktif tenant'a göre otomatik filtreler.
/// Implement etmeyen entity'ler GLOBAL'dir (Country, Currency, Language gibi).
/// </summary>
public interface ITenantScoped
{
    long TenantId { get; set; }
}
```

**`src/SearchConsoleApp.Core/MultiTenancy/ICurrentTenantProvider.cs`**
```csharp
namespace SearchConsoleApp.Core.MultiTenancy;

/// <summary>
/// Aktif tenant'ı çözer. Implementasyon ortama göre değişir:
/// - Web: subdomain / header / JWT claim
/// - Background job: job context
/// - Console: config
/// </summary>
public interface ICurrentTenantProvider
{
    long? CurrentTenantId { get; }
    bool IsAvailable { get; }
}
```

**`src/SearchConsoleApp.Core/Domain/Tenants/Tenant.cs`**
```csharp
namespace SearchConsoleApp.Core.Domain.Tenants;

public partial class Tenant : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";      // subdomain veya path key
    public bool Active { get; set; }
    public bool Deleted { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}
```

### Adım 2: DbContext'i güncelle

**`src/SearchConsoleApp.Data/SearchConsoleAppDbContext.cs`** — `OnModelCreating` içine ekle:

```csharp
using System.Linq.Expressions;
using SearchConsoleApp.Core.MultiTenancy;

public class SearchConsoleAppDbContext : DbContext
{
    private readonly ICurrentTenantProvider _tenantProvider;

    public SearchConsoleAppDbContext(
        DbContextOptions<SearchConsoleAppDbContext> options,
        ICurrentTenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SearchConsoleAppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;

            // ISoftDeletable + ITenantScoped → birleşik filter
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(clr);
            var isTenantScoped  = typeof(ITenantScoped).IsAssignableFrom(clr);

            if (isSoftDeletable || isTenantScoped)
            {
                var p = Expression.Parameter(clr, "e");
                Expression? body = null;

                if (isSoftDeletable)
                {
                    var del = Expression.Property(p, nameof(ISoftDeletable.Deleted));
                    body = Expression.Not(del);
                }

                if (isTenantScoped)
                {
                    // tenantProvider.CurrentTenantId == null ise filtre uygulamaz (admin/raporlama)
                    var tenantId = Expression.Property(p, nameof(ITenantScoped.TenantId));
                    var currentId = Expression.Property(
                        Expression.Constant(_tenantProvider),
                        nameof(ICurrentTenantProvider.CurrentTenantId));
                    var hasValue = Expression.Property(currentId, "HasValue");
                    var value = Expression.Property(currentId, "Value");
                    var matches = Expression.Equal(tenantId, value);
                    var orNoTenant = Expression.OrElse(Expression.Not(hasValue), matches);

                    body = body == null ? orNoTenant : Expression.AndAlso(body, orNoTenant);
                }

                if (body != null)
                {
                    var lambda = Expression.Lambda(body, p);
                    modelBuilder.Entity(clr).HasQueryFilter(lambda);
                }
            }

            if (typeof(BaseEntity).IsAssignableFrom(clr))
                modelBuilder.Entity(clr).HasIndex(nameof(BaseEntity.EntityId)).IsUnique();
        }
    }

    // Insert sırasında TenantId otomatik set et
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (_tenantProvider.CurrentTenantId is long tid)
        {
            foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
            {
                if (entry.State == EntityState.Added && entry.Entity.TenantId == 0)
                    entry.Entity.TenantId = tid;
            }
        }
        return base.SaveChangesAsync(ct);
    }
}
```

### Adım 3: Tenant resolver'ı implement et (Web)

**`src/SearchConsoleApp.Web.Framework/MultiTenancy/HttpCurrentTenantProvider.cs`**
```csharp
using Microsoft.AspNetCore.Http;
using SearchConsoleApp.Core.MultiTenancy;
using SearchConsoleApp.Core.Infrastructure.DependencyManagement;

public class HttpCurrentTenantProvider : ICurrentTenantProvider, IScopedService
{
    private readonly IHttpContextAccessor _http;
    private long? _resolved;
    private bool _cached;

    public HttpCurrentTenantProvider(IHttpContextAccessor http) => _http = http;

    public long? CurrentTenantId
    {
        get
        {
            if (_cached) return _resolved;
            _resolved = Resolve();
            _cached = true;
            return _resolved;
        }
    }

    public bool IsAvailable => CurrentTenantId.HasValue;

    private long? Resolve()
    {
        var ctx = _http.HttpContext;
        if (ctx == null) return null;

        // ÖRNEK 1: Subdomain → tenant slug → ID
        // var host = ctx.Request.Host.Host;
        // var slug = host.Split('.')[0];
        // ... ITenantService.GetBySlugAsync(slug)

        // ÖRNEK 2: Header
        if (ctx.Request.Headers.TryGetValue("X-Tenant-Id", out var v)
            && long.TryParse(v, out var id)) return id;

        // ÖRNEK 3: JWT claim
        var claim = ctx.User?.FindFirst("tenant_id")?.Value;
        if (long.TryParse(claim, out var jwtId)) return jwtId;

        return null;
    }
}
```

### Adım 4: Service'lerde kullanım

Tenant filter **otomatik çalıştığı için** service kodu değişmez. Ama bazı durumlarda manuel müdahale gerekir:

```csharp
// Cross-tenant rapor (admin):
var all = await _repository.Table
    .IgnoreQueryFilters()
    .Where(o => o.CreatedOnUtc > since)
    .ToListAsync();

// Belirli tenant'ı çek:
var orders = await _repository.Table
    .IgnoreQueryFilters()
    .Where(o => o.TenantId == specificTenantId)
    .ToListAsync();
```

### Adım 5: Cache key'lerine tenant'ı dahil et

```csharp
public virtual async Task<Customer?> GetCustomerByIdAsync(long customerId)
{
    var tenant = _tenantProvider.CurrentTenantId ?? 0;
    var key = CustomerByIdKey.Create(tenant, customerId);  // "SearchConsoleApp.customer.byid.{0}.{1}"
    return await _cacheManager.GetAsync(key,
        async () => await _customerRepository.GetByIdAsync(customerId));
}
```

**Cache key formatı:** `SearchConsoleApp.<tenant>.<entity>.<usage>.{args}`

---

## Senaryo B — DB-per-Tenant + Factory

Tenant başına ayrı DB. Senaryo A'daki `ITenantScoped` ve query filter **YOK** — onun yerine DbContext factory ile her isteğe doğru DB seçilir.

### Adım 1: Tenant store

`Tenant` entity'sine `ConnectionString` (encrypted) ekle. Bu kayıtlar **master DB**'de tutulur.

### Adım 2: DbContext factory

**`src/SearchConsoleApp.Data/SearchConsoleAppDbContextFactory.cs`**
```csharp
public interface ISearchConsoleAppDbContextFactory
{
    Task<SearchConsoleAppDbContext> CreateAsync();
}

public class SearchConsoleAppDbContextFactory : ISearchConsoleAppDbContextFactory, IScopedService
{
    private readonly ICurrentTenantProvider _tenantProvider;
    private readonly ITenantRegistry _registry;  // master DB'den ConnectionString getirir

    public async Task<SearchConsoleAppDbContext> CreateAsync()
    {
        var tid = _tenantProvider.CurrentTenantId
            ?? throw new InvalidOperationException("Tenant required");
        var conn = await _registry.GetConnectionStringAsync(tid);
        var opts = new DbContextOptionsBuilder<SearchConsoleAppDbContext>()
            .UseSqlServer(conn).Options;
        return new SearchConsoleAppDbContext(opts);
    }
}
```

### Adım 3: Repository factory'i kullanır

```csharp
public class EfRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    private readonly ISearchConsoleAppDbContextFactory _factory;
    // ... her method'da: await using var ctx = await _factory.CreateAsync();
}
```

### Migration stratejisi

Yeni tenant onboard edildiğinde:
1. Master DB'ye `Tenant` kaydı oluştur (connection string ile)
2. Yeni DB'yi yarat (`CREATE DATABASE`)
3. `SearchConsoleAppDbContext.Database.MigrateAsync()` ile şemayı uygula
4. Seed data çalıştır

```csharp
public async Task ProvisionTenantAsync(Tenant tenant)
{
    using var ctx = new SearchConsoleAppDbContext(BuildOptions(tenant.ConnectionString));
    await ctx.Database.MigrateAsync();
    // seed...
}
```

---

## Hangi Entity Tenant'a Ait?

Şu soruyu sor: **"Tenant A bu kaydı görmeli mi, Tenant B görmemeli mi?"**

| Entity | ITenantScoped? | Sebep |
|---|---|---|
| `Customer`, `Order`, `Product` | ✅ Evet | Tenant'a özel iş verisi |
| `Setting` (tenant ayarları) | ✅ Evet | Her tenant kendi config'i |
| `Country`, `Currency`, `Language` | ❌ Hayır | Global lookup tablosu |
| `Tenant` entity'sinin kendisi | ❌ Hayır | Master tablo |
| `User` (sistem admini) | ❌ Hayır | Tenant'lar üstü |
| `User` (tenant kullanıcısı) | ✅ Evet | Tenant'a bağlı |

---

## Test Stratejisi

**Senaryo A:**
- Birim test: `ICurrentTenantProvider`'ı mock'la, farklı tenant ID'leri için ayrı test case'leri yaz.
- Integration test: aynı DB, iki ayrı tenant context, "X tenant'ı Y'nin verisini göremiyor" assertion'ı.

**Senaryo B:**
- Tenant başına test DB'si veya SQLite in-memory.
- Provisioning testleri ayrı kategori.

---

## Yaygın Tuzaklar

❌ **Query filter'ı bypass etmek** — `IgnoreQueryFilters()` ihtiyaç olmadıkça yasak. Bypass eden her sorgu code review'da gerekçelendirilmeli.

❌ **Cache key'e tenant'ı dahil etmemek** — `Customer:byid:5` farklı tenant'larda farklı kayıt döndürebilir → veri sızıntısı.

❌ **Update'te `TenantId` değiştirmek** — entity'nin tenant değişimi YASAK. Gerekiyorsa yeni kayıt oluştur.

❌ **`ITenantScoped` olmayan entity'de tenant kontrolü** — global entity'lere tenant filtresi koyma; UI'da yetkilendirme ile çöz.

❌ **Subdomain resolver'ı her request'te DB'ye bakmak** — `IMemoryCache` ile slug→tenantId map'ini cache'le.

❌ **Background job'da `ICurrentTenantProvider`'ın boş gelmesi** — job context'inden tenant'ı manuel set eden bir `TenantScope` mekanizması yaz.

---

## Geçiş: Single → Multi

Mevcut single-tenant uygulamayı multi'ye çevirmek:

1. Yeni `Tenant` tablosu oluştur, "default" diye tek kayıt insert et.
2. Tenant'a ait olacak tablolara `TenantId` kolonu ekle, default = 1.
3. Entity'lere `ITenantScoped` ekle.
4. DbContext'i Senaryo A'daki gibi güncelle.
5. Tenant resolver'ı subdomain/header'a bağla.
6. Cache key'lerine tenant'ı dahil et (en kritik adım, atlama).
7. Tüm sorguları gözden geçir — `IgnoreQueryFilters` kullananları işaretle.
