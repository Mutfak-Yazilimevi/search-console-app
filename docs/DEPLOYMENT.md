# Deployment & Operations

## Docker Compose (Dev)

Tek komutla tam stack:

```bash
docker compose up -d
```

Servisler:

| Service | Port | Açıklama |
|---|---|---|
| `api` | 5000 | SearchConsoleApp.Web (.NET 9 backend) |
| `sql` | 1433 | SQL Server 2022 Express |
| `redis` | 6379 | Redis 7 |
| `seq` | 8081 | Serilog log explorer (UI: http://localhost:8081) |
| `jaeger` | 16686 | Distributed tracing UI (http://localhost:16686) |
| `mailpit` | 8025 | SMTP test (UI: http://localhost:8025) |

Mailpit ile gönderilen tüm email'leri tarayıcıdan görüntüleyebilirsin. Gerçek SMTP'ye bağlanmaz.

**Reset DB:**
```bash
docker compose down -v   # volumes dahil sil
docker compose up -d
```

**Logs:**
```bash
docker compose logs -f api
```

## Production Build

### Backend image

```bash
docker build -t SearchConsoleApp-api:latest .
docker run -d -p 5000:8080 \
  -e ConnectionStrings__Default="..." \
  -e Jwt__Key="$(openssl rand -base64 48)" \
  SearchConsoleApp-api:latest
```

**Image size:** ~110MB (Alpine + trimmed runtime).
**Non-root:** `app` user (UID 1000), `/app` working directory.
**Healthcheck:** `wget /health/live` her 30s.

### Frontend image

Her app için ayrı build:

```bash
docker build -f frontend/Dockerfile --build-arg APP=web-app -t SearchConsoleApp-web:latest .
docker build -f frontend/Dockerfile --build-arg APP=admin-app -t SearchConsoleApp-admin:latest .
docker build -f frontend/Dockerfile --build-arg APP=public-app -t SearchConsoleApp-public:latest .
```

Nginx ile serve edilir, SPA fallback aktif (deep link'ler index.html'e gider).

## CI/CD — GitHub Actions

### `.github/workflows/ci.yml` (her PR + main push)

- **Backend job**: restore → build → test → coverage report
- **Frontend job**: Nx affected ile lint + test, sonra tüm app'ler build
- **Docker job** (sadece main): API + web + admin image'lar GHCR'a push

### `.github/workflows/cd.yml` (CI başarılı sonrası)

Placeholder — gerçek deploy hedefine göre düzenle:
- Azure App Service
- AWS ECS
- Kubernetes (kubectl)
- SSH + docker compose

Manuel trigger için `workflow_dispatch` ile staging/production seçimi.

## Production Checklist

### Güvenlik
- [ ] `Jwt:Key` env var'dan gelmeli (en az 32 karakter rastgele)
- [ ] `appsettings.json`'da SECRET YOK — env / Azure Key Vault / AWS Secrets Manager
- [ ] HTTPS zorunlu (reverse proxy veya `app.UseHttpsRedirection`)
- [ ] CORS production domain'leri ile sınırlı (`Cors:AllowedOrigins`)
- [ ] Rate limit limitleri production trafiğine göre ayarlanmış
- [ ] `Email:Mode = "smtp"` (production'da `log` değil)
- [ ] Default admin parolası değiştirilmiş veya disable edilmiş

### Performance
- [ ] `Cache:Provider = "redis"` — distributed cache aktif
- [ ] DB connection pooling configured (default EF ile gelir)
- [ ] Static file caching nginx tarafında (CDN önerilir)
- [ ] OpenTelemetry endpoint'i ayarlı (`Observability:OtlpEndpoint`)

### Observability
- [ ] Serilog → Seq / Elastic / CloudWatch
- [ ] OpenTelemetry → Jaeger / Tempo / Honeycomb
- [ ] Health endpoint k8s liveness/readiness'e bağlı
- [ ] Alerting: `/health/ready` 503 olduğunda alert

### Veri
- [ ] DB backup stratejisi (günlük + point-in-time)
- [ ] Migration apply automation (CD'de `dotnet ef database update`)
- [ ] AuditLog retention politikası production'a uygun
- [ ] Blob storage S3'e geçirilmiş (production'da local DEĞİL)

### Multi-instance
- [ ] `IPreAuthTokenStore` Redis-backed (in-memory değil)
- [ ] Rate limiter Redis-backed (in-memory'de instance başı limit)
- [ ] Local blob storage NFS/EFS'e mount veya S3'e geçilmiş
- [ ] Sticky session GEREKMEZ (JWT stateless) — ama refresh token DB'de

## Integration Tests

```bash
dotnet test tests/SearchConsoleApp.IntegrationTests/SearchConsoleApp.IntegrationTests.csproj
```

**Test fixture:**
- DB: EF InMemory (her test class'ı izole)
- Email: `TestEmailSender` (gönderilen mesajları assert et)
- GeoIP: NoOp
- Rate limit: bypass

`SearchConsoleAppWebApplicationFactory.ResetDatabaseAsync()` her test'in başında DB'yi sıfırlar.

**Test ekleme:**
```csharp
public class MyFeatureTests : IClassFixture<SearchConsoleAppWebApplicationFactory>, IAsyncLifetime
{
    private readonly SearchConsoleAppWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MyFeatureTests(SearchConsoleAppWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task My_test() { ... }
}
```

## API Versioning

URL segment: `/api/v1/public/auth/login`

**Yeni version eklemek:**

```csharp
// SearchConsoleApp.Web/Controllers/V2/Web/AccountController.cs
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/web/[controller]")]
public class AccountController : WebApiController
{
    [HttpGet("profile")]
    public IActionResult Profile() => Ok(new { /* v2 schema */ });
}
```

**Deprecate eski version:**
```csharp
[ApiVersion("1.0", Deprecated = true)]
public class OldController : WebApiController { }
```

Response header: `api-supported-versions: 1.0, 2.0`, `api-deprecated-versions: 1.0`.

**Frontend client:**
- `apiRootUrl: 'http://localhost:5000/api/v1'`
- v2'ye geçişte: hem v1 hem v2 çağıran client gradient olur, breaking change yumuşatılır

## Blob Storage

### Local (dev)

```json
"Blob": {
  "Provider": "local",
  "Local": {
    "RootPath": "App_Data/blobs",
    "PublicBaseUrl": "http://localhost:5000/blobs"
  }
}
```

Dosyalar `App_Data/blobs/` altında, `/blobs/...` URL'i ile serve edilir.

### S3 / S3-uyumlu (prod)

```json
"Blob": {
  "Provider": "s3",
  "S3": {
    "BucketName": "SearchConsoleApp-blobs",
    "Region": "eu-central-1",
    "ServiceUrl": "",                       // boş = AWS
    "PublicBaseUrl": "https://cdn.SearchConsoleApp.com" // varsa CDN URL, yoksa presigned
  }
}
```

**S3-uyumlu sağlayıcılar** (`ServiceUrl` set ederek):
- MinIO: `http://minio:9000`
- Cloudflare R2: `https://<account>.r2.cloudflarestorage.com`
- DigitalOcean Spaces: `https://<region>.digitaloceanspaces.com`
- Wasabi: `https://s3.wasabisys.com`

**Credentials**: production'da env var veya IAM role kullan, appsettings'e gömme:
- `Blob__S3__AccessKey`
- `Blob__S3__SecretKey`

**Public URL stratejisi:**
- `PublicBaseUrl` set ise → static URL (object'lerin public-read olması gerek, CDN arkasında)
- Yoksa → presigned URL (TTL'li, geçici)

### Upload örneği

```bash
curl -X POST http://localhost:5000/api/v1/web/files/upload \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@./photo.jpg"

# Response:
# { "key": "customers/42/abc-photo.jpg", "url": "...", "size": 123456 }
```
