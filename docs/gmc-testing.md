# Merchant Center ürün uyumluluğu — test rehberi

## Yerel ortam

1. `.env` dosyasını `.env.example` üzerinden oluşturun.
2. Gerekli servisler: `docker compose up -d sql redis api crawl-worker`
3. Frontend: `nx serve public-app` → http://localhost:4200/merchant-center

## Site-only analiz (GMC bağlantısı olmadan)

1. `/merchant-center` sayfasında mağaza URL'si girin (ör. `https://ahmetfaikderi.com/`).
2. **Analiz Et** — crawl worker ürün sayfalarını tarar, webhook ile API'ye gönderir.
3. Tamamlandığında kontrol edin:
   - Uyumluluk skoru ve ürün tablosu
   - Öncelikli aksiyonlar, site / feed / çapraz ürün sorunları
   - JSON / HTML rapor / **Checklist (Markdown)** indirme
   - Sayfa URL'si `?run={entityId}` ile paylaşılabilir (yenilemede sonuç korunur)
   - Giriş yapmadan analiz yaptıysanız **Son analizler** tarayıcı localStorage'da tutulur
   - (Opsiyonel) Top 5 başlık AI, ürün detayında yeniden tarama

## Entegrasyonlar

Sayfa üstündeki chip'ler `GET /api/v1/public/merchant-center/compliance/integrations/status` ile yüklenir (crawl-worker, PageSpeed, Safe Browsing, Gemini, GMC OAuth).

| Anahtar | Etki |
|---------|------|
| `Google:PageSpeedApiKey` | En sorunlu 5 ürün URL'sinde mobil PSI (`GMC-PERF-*`) |
| `Google:SafeBrowsingApiKey` | Site düzeyi `GMC-SAFE-001` |
| `Gemini` / LLM anahtarı | AI özet, generate, explain, bulk başlık |

## GMC OAuth (feed + performans)

Giriş yapmış kullanıcılar **Son analizleriniz** chip'lerinden önceki çalıştırmalara dönebilir (`GET /api/v1/web/merchant-center/compliance/runs`).

1. GCP: [Merchant API](https://console.cloud.google.com/apis/library/merchantapi.googleapis.com) etkin.
2. Merchant Center → **Settings → API** → `registerGcp` ile GCP projesini kaydedin.
3. OAuth consent: `https://www.googleapis.com/auth/content` scope.
4. `.env`: `GOOGLE_GMC_CLIENT_ID`, `GOOGLE_GMC_CLIENT_SECRET`, `GOOGLE_GMC_REDIRECT_URI`.
5. Uygulamada giriş yapın → **Merchant Center'a Bağlan** → callback `/auth/merchant-center/callback`.
6. GMC hesabı seçerek **Analiz Et** — feed özeti, `product_performance_view` (son 30 gün, top 10 tıklama), feed eşleştirme sorunları (`GMC-FEED-001/002`) gelmeli.
7. API hatasında **Feed eşleştirme** bölümünde `GMC-WARN-001` uyarısı görünür; site analizi sonuçları korunur.

## Otomatik testler

```bash
dotnet test tests/SearchConsoleApp.IntegrationTests --filter "FullyQualifiedName~ProductCompliance|FullyQualifiedName~GmcSpec"
```

Kapsam: spec validator kuralları, webhook akışı, HTML export.

## Bilinen sınırlar

- İptal, worker'daki devam eden fetch'i anında durdurmaz; bir sonraki döngüde veya complete öncesi kontrol edilir.
- Takılı kalan analizler `ProductComplianceStaleRunWorker` ile audit ile aynı timeout kurallarında Failed yapılır.
- `product_performance_view` yalnızca GMC'de yeterli Shopping performans verisi olan hesaplarda dolu gelir.
- Site-only mod gerçek Merchant Center onay/red kararını yansıtmaz.
