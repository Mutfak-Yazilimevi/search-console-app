import { RULE_CATALOG, type RuleCatalogEntry } from './rule-catalog.generated';

export interface RulePlaybookEntry {
  summary: string[];
  example: string;
}

function catalogToPlaybook(entry: RuleCatalogEntry): RulePlaybookEntry {
  const summary: string[] = [];
  if (entry.fixHint) summary.push(entry.fixHint);
  if (entry.message && !summary.includes(entry.message)) summary.push(entry.message);
  return {
    summary,
    example: entry.docUrl
      ? `Kaynak: ${entry.docUrl}`
      : 'Detaylar için Google Search Central dokümantasyonuna bakın.',
  };
}

/** Kural koduna göre özet öneriler ve uygulama örnekleri. */
export const RULE_PLAYBOOK: Record<string, RulePlaybookEntry> = {
  'meta-title-missing': {
    summary: [
      'Her sayfada benzersiz bir <title> olmalı.',
      'Başlık sayfa konusunu net anlatmalı (ideal 50–60 karakter).',
      'Marka adını genelde sonda kullanın: "Konu | Site Adı".',
    ],
    example: `<head>
  <title>SEO Site Denetimi — Ücretsiz Tarama | ÖrnekApp</title>
</head>`,
  },
  'meta-description-missing': {
    summary: [
      'Arama sonuçlarında görünen kısa özeti meta description ile verin.',
      '1–2 cümle, 120–160 karakter hedefleyin.',
      'Her sayfa için farklı açıklama yazın.',
    ],
    example: `<meta name="description"
  content="Sitenizi Google kurallarına göre tarayın; kritik SEO hatalarını ve düzeltme önerilerini görün.">`,
  },
  'https-required': {
    summary: [
      'Tüm sayfalar HTTPS ile sunulmalı.',
      'HTTP isteklerini 301 ile HTTPS\'e yönlendirin.',
      'Geçerli ve güncel TLS sertifikası kullanın.',
    ],
    example: `# nginx örneği
server {
  listen 80;
  server_name ornek.com;
  return 301 https://$host$request_uri;
}`,
  },
  'title-too-short': {
    summary: [
      'Başlığı konuyu yansıtacak şekilde genişletin.',
      'Ana anahtar kelimeyi doğal biçimde ekleyin.',
      '10 karakterden kısa başlıklar genelde yetersiz bilgi verir.',
    ],
    example: `<!-- Kısa -->
<title>Denetim</title>

<!-- Daha iyi -->
<title>Ücretsiz SEO Site Denetimi ve Hata Raporu</title>`,
  },
  'title-too-long': {
    summary: [
      'Başlığı 60 karakter civarında tutun.',
      'En önemli kelimeleri başa alın.',
      'Gereksiz tekrar ve doldurma kelimelerden kaçının.',
    ],
    example: `<!-- Uzun (kesilebilir) -->
<title>SEO, arama motoru optimizasyonu, Google sıralama, site analizi...</title>

<!-- Kısa ve net -->
<title>SEO Site Denetimi | ÖrnekApp</title>`,
  },
  'viewport-missing': {
    summary: [
      'Mobil uyumluluk için viewport meta etiketi zorunludur.',
      'width=device-width mobil cihazda doğru ölçek sağlar.',
      'Google mobil öncelikli indeksleme kullanır.',
    ],
    example: `<meta name="viewport" content="width=device-width, initial-scale=1">`,
  },
  'canonical-missing': {
    summary: [
      'Aynı içeriğin birden fazla URL\'de görünmesini canonical ile birleştirin.',
      'Tercih ettiğiniz (ana) URL\'yi belirtin.',
      'Parametreli veya www/non-www kopyalar için özellikle önemli.',
    ],
    example: `<link rel="canonical" href="https://ornek.com/hizmetler/seo-denetimi">`,
  },
  'h1-missing': {
    summary: [
      'Sayfa başına tek bir ana H1 kullanın.',
      'H1, sayfanın ana konusunu kullanıcıya net söylemeli.',
      'Title ile uyumlu ama birebir kopya olmak zorunda değil.',
    ],
    example: `<main>
  <h1>SEO Site Denetimi</h1>
  <p>Sitenizi tarayın, hataları görün...</p>
</main>`,
  },
  'h1-multiple': {
    summary: [
      'Bir sayfada yalnızca bir H1 olmalı — genelde sayfa konusunu özetleyen ana başlık.',
      'Diğer H1\'leri H2 (bölüm) veya H3 (kart/widget) olarak düşürün.',
      'Logo, menü ve tekrarlayan bileşenlerde H1 kullanmayın.',
      'Detay tablosunda her H1 metni ve önerilen etiket listelenir.',
    ],
    example: `<h1>SEO Denetimi</h1>
<h2>Nasıl çalışır?</h2>
<h2>Sık sorulan sorular</h2>`,
  },
  'noindex-detected': {
    summary: [
      'noindex sayfanın Google\'da listelenmesini engeller.',
      'Yayınlanması gereken sayfalardan noindex\'i kaldırın.',
      'Meta etiket, HTTP başlığı ve robots.txt kurallarını kontrol edin.',
    ],
    example: `<!-- İndekslenmesini istiyorsanız kaldırın -->
<meta name="robots" content="index, follow">`,
  },
  'http-status-error': {
    summary: [
      '404/500 gibi hatalar kullanıcı ve bot deneyimini bozar.',
      'Kırık dahili bağlantıları düzeltin veya 301 yönlendirin.',
      'Sunucu loglarından hata URL\'lerini tespit edin.',
    ],
    example: `# Kalıcı taşınma
Redirect 301 /eski-sayfa https://ornek.com/yeni-sayfa`,
  },
  'img-alt-missing': {
    summary: [
      'Anlamlı görsellere açıklayıcı alt metni ekleyin.',
      'Dekoratif görsellerde alt="" kullanılabilir.',
      'Anahtar kelime doldurma yapmayın; görseli tarif edin.',
    ],
    example: `<img src="/urun.jpg"
     alt="Mavi koşu ayakkabısı, yan profil"
     width="600" height="400">`,
  },
  'robots-txt-missing': {
    summary: [
      '/robots.txt dosyası tarayıcılara site kurallarını iletir.',
      'Site haritası konumunu burada belirtin.',
      'Gereksiz yere tüm siteyi engellemeyin.',
    ],
    example: `User-agent: *
Allow: /

Sitemap: https://ornek.com/sitemap.xml`,
  },
  'sitemap-missing': {
    summary: [
      'Önemli URL\'leri içeren sitemap.xml oluşturun.',
      'Search Console\'a gönderin.',
      'Düzenli güncelleyin (yeni sayfa, kaldırılan URL).',
    ],
    example: `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>https://ornek.com/</loc>
    <lastmod>2026-06-07</lastmod>
  </url>
</urlset>`,
  },
  'js-title-missing-static': {
    summary: [
      'Google ilk HTML\'i okur; title sonradan JS ile gelmemeli.',
      'SSR, SSG veya prerender kullanın.',
      'Angular/React\'te title meta sunucu tarafında render edilmeli.',
    ],
    example: `<!-- İlk HTML'de -->
<title>Ürün Detayı — Örnek Mağaza</title>
<!-- JS ile document.title = ... yerine SSR tercih edin -->`,
  },
  'js-title-changed': {
    summary: [
      'Ham HTML ile render sonrası title aynı olmalı.',
      'Gereksiz client-side title değişiminden kaçının.',
      'Route değişiminde tutarlı başlık stratejisi kullanın.',
    ],
    example: `// Her route için tek kaynak
export const routes = [
  { path: 'denetim', title: 'SEO Denetimi | ÖrnekApp' },
];`,
  },
  'js-h1-missing-static': {
    summary: [
      'Ana başlık ilk HTML\'de görünür olmalı.',
      'SSR/SSG ile H1\'i şablona gömün.',
      'Boş div\'e sonradan H1 enjekte etmeyin.',
    ],
    example: `<!-- SSR çıktısı -->
<app-root>
  <h1>Hizmetlerimiz</h1>
</app-root>`,
  },
  'js-hash-routing': {
    summary: [
      '#/sayfa yerine gerçek path kullanın: /sayfa',
      'Angular\'da PathLocationStrategy (varsayılan) tercih edin.',
      'Dahili linklerde href="/gercek-url" kullanın.',
    ],
    example: `<!-- Kötü -->
<a href="#/hakkimizda">Hakkımızda</a>

<!-- İyi -->
<a href="/hakkimizda">Hakkımızda</a>`,
  },
  'link-not-crawlable': {
    summary: [
      'javascript: href tarayıcı tarafından takip edilmez.',
      'Gerçek URL ile <a href> kullanın.',
      'onclick ile navigasyon yerine link tercih edin.',
    ],
    example: `<!-- Kötü -->
<a href="javascript:go('/urun')">Ürün</a>

<!-- İyi -->
<a href="/urun">Ürün</a>`,
  },
  'json-ld-invalid': {
    summary: [
      'JSON-LD geçerli JSON olmalı (virgül, tırnak hatalarına dikkat).',
      'Zengin Sonuçlar Testi ile doğrulayın.',
      'Dinamik üretimde kaçış karakterlerini kontrol edin.',
    ],
    example: `<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "Organization",
  "name": "Örnek Şirket",
  "url": "https://ornek.com"
}
</script>`,
  },
  'json-ld-missing': {
    summary: [
      'Sayfa türüne uygun schema.org JSON-LD ekleyin.',
      'Article, Product, LocalBusiness vb. seçin.',
      'Görünür içerikle şema verisi tutarlı olmalı.',
    ],
    example: `<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "WebSite",
  "name": "ÖrnekApp",
  "url": "https://ornek.com"
}
</script>`,
  },
  'data-vocabulary-deprecated': {
    summary: [
      'data-vocabulary.org artık desteklenmiyor.',
      'schema.org JSON-LD formatına geçin.',
      'Eski microdata işaretlemelerini temizleyin.',
    ],
    example: `<!-- Eski (kaldırın) -->
<div itemscope itemtype="http://data-vocabulary.org/Product">

<!-- Yeni -->
<script type="application/ld+json">
{ "@type": "Product", "name": "..." }
</script>`,
  },
  'og-title-missing': {
    summary: [
      'Sosyal paylaşım ve Discover için og:title ekleyin.',
      'Genelde sayfa title ile aynı veya kısaltılmış olabilir.',
      'Her sayfada benzersiz olmalı.',
    ],
    example: `<meta property="og:title" content="SEO Site Denetimi | ÖrnekApp">
<meta property="og:type" content="website">`,
  },
  'og-image-missing': {
    summary: [
      'Paylaşım önizlemesi için og:image ekleyin.',
      'Minimum 1200 px genişlik önerilir.',
      'HTTPS URL kullanın.',
    ],
    example: `<meta property="og:image" content="https://ornek.com/assets/og-kapak.jpg">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">`,
  },
  'og-image-small': {
    summary: [
      'Görseli en az 1200 px genişliğe yükseltin.',
      'Yüksek çözünürlüklü, net bir kapak görseli seçin.',
      'Metin içeren görsellerde okunabilirliğe dikkat edin.',
    ],
    example: `<meta property="og:image" content="https://ornek.com/og-1200x630.jpg">`,
  },
  'x-robots-noindex-header': {
    summary: [
      'X-Robots-Tag: noindex HTTP başlığı sayfayı indeks dışı bırakır.',
      'Yayınlanması gereken sayfalarda bu başlığı kaldırın.',
      'CDN veya sunucu yapılandırmasını kontrol edin.',
    ],
    example: `# İndekslenmesi gereken sayfa için noindex göndermeyin
# X-Robots-Tag: noindex  ← kaldırın`,
  },
  'hreflang-missing-x-default': {
    summary: [
      'Çok dilli sitelerde x-default yedek dili belirtir.',
      'Diğer hreflang etiketleriyle birlikte kullanın.',
      'Genelde ana veya dil seçim sayfasına işaret eder.',
    ],
    example: `<link rel="alternate" hreflang="tr" href="https://ornek.com/tr/" />
<link rel="alternate" hreflang="en" href="https://ornek.com/en/" />
<link rel="alternate" hreflang="x-default" href="https://ornek.com/" />`,
  },
  'hreflang-missing-return': {
    summary: [
      'Her dil sayfası diğer tüm alternatiflere link vermeli.',
      'Karşılıklı (reciprocal) hreflang zorunludur.',
      'Search Console uluslararası raporunu kontrol edin.',
    ],
    example: `<!-- tr sayfasında -->
<link rel="alternate" hreflang="en" href="https://ornek.com/en/sayfa" />
<!-- en sayfasında da tr linki olmalı -->`,
  },
  'html-lang-missing': {
    summary: [
      '<html lang="..."> arama motoruna sayfa dilini söyler.',
      'Türkçe içerik için lang="tr" kullanın.',
      'Bölgesel varyant için tr-TR gibi kodlar mümkün.',
    ],
    example: `<!doctype html>
<html lang="tr">
<head>...</head>`,
  },
  'geo-faq-missing': {
    summary: [
      'Sayfayla ilgili 3–5 gerçek soru-cevap ekleyin.',
      'Sorular kullanıcı niyetini yansıtmalı; cevaplar kısa ve net olmalı.',
      'Görünür SSS ile JSON-LD FAQPage şemasını eşleştirin.',
      'Ana sayfa, hizmet ve blog yazıları için ayrı SSS düşünün.',
    ],
    example: `<section id="sss">
  <h2>Sık Sorulan Sorular</h2>
  <h3>SEO denetimi ne kadar sürer?</h3>
  <p>Site büyüklüğüne göre birkaç dakika ile yarım saat arasında değişir.</p>
</section>

<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "FAQPage",
  "mainEntity": [{
    "@type": "Question",
    "name": "SEO denetimi ne kadar sürer?",
    "acceptedAnswer": {
      "@type": "Answer",
      "text": "Site büyüklüğüne göre birkaç dakika ile yarım saat arasında değişir."
    }
  }]
}
</script>`,
  },
  'geo-summary-missing': {
    summary: [
      'Sayfanın en üstüne 2–3 cümlelik net bir özet ekleyin.',
      'Özet ana arama sorusunu doğrudan yanıtlamalı.',
      'Sonra detaylı içeriğe geçin.',
    ],
    example: `<h1>SEO Site Denetimi</h1>
<p class="lead">
  Sitenizin adresini girin; Google Search Central kurallarına göre
  taranır, kritik hatalar ve düzeltme önerileri raporlanır.
</p>`,
  },
  'geo-ai-disclosure': {
    summary: [
      'Yapay zekâ ile üretilen veya düzenlenen içeriği açıkça belirtin.',
      'Doğruluk için insan editörü süreci tanımlayın.',
      'Yanıltıcı veya otomatik üretilmiş düşük kalite içerikten kaçının.',
    ],
    example: `<p class="ai-disclosure">
  Bu makale yapay zekâ desteğiyle hazırlanmış; içerik editörümüz
  tarafından doğrulanmıştır.
</p>`,
  },
  'dup-title': {
    summary: [
      'Her URL benzersiz bir title\'a sahip olmalı.',
      'Liste/kategori sayfalarında sayfa numarası ekleyebilirsiniz.',
      'CMS şablonlarında otomatik tekrar oluşumunu kontrol edin.',
    ],
    example: `<!-- Sayfa 1 -->
<title>Blog Yazıları | ÖrnekApp</title>
<!-- Sayfa 2 -->
<title>Blog Yazıları — Sayfa 2 | ÖrnekApp</title>`,
  },
  'dup-description': {
    summary: [
      'Her sayfa için özgün meta description yazın.',
      'Şablon metin kopyalamaktan kaçının.',
      'Sayfa içeriğine özel 1–2 cümle yeterli.',
    ],
    example: `<!-- Ürün A -->
<meta name="description" content="Mavi koşu ayakkabısı, hafif taban, 42 numara stokta.">
<!-- Ürün B — farklı açıklama -->
<meta name="description" content="Su geçirmez trekking botu, kış sezonu için ideal.">`,
  },
  'orphan-page': {
    summary: [
      'Yetim sayfalar site içi bağlantı bulamayan URL\'lerdir.',
      'Menü, footer veya ilgili içerikten link verin.',
      'Site haritasına eklemeyi unutmayın.',
    ],
    example: `<!-- İlgili yazıdan link -->
<p>Daha fazla bilgi için
  <a href="/rehber/seo-denetimi">SEO denetimi rehberi</a>.
</p>`,
  },
  'SC-001': {
    summary: [
      'Siteyi Google Search Console\'da mülk olarak ekleyin.',
      'DNS, HTML dosyası veya Analytics ile doğrulayın.',
      'Tarama yaptığınız URL ile SC mülkü eşleşmeli (http/https, www).',
    ],
    example: `1. search.google.com/search-console
2. Mülk ekle → https://ornek.com
3. Doğrulama yöntemini tamamlayın
4. Bağlı mod ile yeniden tarayın`,
  },
  'RANK-001': {
    summary: [
      'Son 28 günde gösterim yoksa indeks veya relevans sorunu olabilir.',
      'URL Denetimi ile indeks durumunu kontrol edin.',
      'İçerik kalitesi ve hedef anahtar kelime uyumunu gözden geçirin.',
    ],
    example: `Search Console → URL Denetimi → URL yapıştır
→ "Canlı test" / indeks durumu raporunu inceleyin`,
  },
  'INDEX-001': {
    summary: [
      'Google\'ın bildirdiği indeks engelini giderin.',
      'noindex, robots.txt, canonical ve kalite sorunlarını kontrol edin.',
      'Düzeltme sonrası "İndeksleme iste" gönderin.',
    ],
    example: `<!-- Engelse -->
<meta name="robots" content="noindex">

<!-- Düzeltme -->
<meta name="robots" content="index, follow">`,
  },
  'RICH-001': {
    summary: [
      'Search Console\'daki yapılandırılmış veri hatalarını düzeltin.',
      'Zengin Sonuçlar Testi ile doğrulayın.',
      'Zorunlu alanların eksiksiz olduğundan emin olun.',
    ],
    example: `https://search.google.com/test/rich-results
→ Hatalı URL\'yi test edin → eksik alanları tamamlayın`,
  },
  'SAFE-001': {
    summary: [
      'Safe Browsing uyarısı ciddi güvenlik sorununa işaret eder.',
      'Kötü amaçlı kod, phishing veya istenmeyen yazılımı temizleyin.',
      'Search Console Güvenlik sorunları raporunu takip edin.',
    ],
    example: `1. Search Console → Güvenlik ve manuel işlemler
2. Etkilenen URL\'leri temizleyin
3. İnceleme talebi gönderin`,
  },
  'CWV-004': {
    summary: [
      'LCP: ana içeriği hızlı gösterin (büyük görsel/CSS optimizasyonu).',
      'CLS: boyut rezervasyonu ile düzen kaymasını azaltın.',
      'Render-blocking JS/CSS\'i ertele veya küçült.',
    ],
    example: `<!-- Görsel boyutları belirtin -->
<img src="hero.webp" width="1200" height="630" fetchpriority="high">

<!-- Kritik CSS inline, geri kalanı async -->`,
  },
  'EEAT-001': {
    summary: [
      'Yazar biyografisi ve uzmanlık sinyalleri ekleyin.',
      'Kaynak gösterin; güncelleme tarihi belirtin.',
      'Özgün, insan odaklı içerik üretin.',
    ],
    example: `<article>
  <p>Yazar: <a href="/yazar/ahmet">Ahmet Yılmaz</a>, SEO uzmanı</p>
  <p>Son güncelleme: 7 Haziran 2026</p>
  ...
</article>`,
  },
  'redirect-chain-long': {
    summary: [
      'Yönlendirme zincirlerini kısaltın — hedef URL\'ye doğrudan 301 kullanın.',
      'HTTP → www → trailing slash gibi çoklu atlama Core Web Vitals\'ı kötüleştirir.',
      'Sunucu/nginx/Cloudflare kurallarını gözden geçirin.',
    ],
    example: `# Doğrudan hedefe 301
return 301 https://www.ornek.com/sayfa;`,
  },
  'canonical-multiple': {
    summary: [
      'Sayfa başına yalnızca bir canonical kullanın.',
      'Çakışan canonical\'lar indeks karışıklığına yol açar.',
      'Self-referencing canonical tercih edin.',
    ],
    example: `<link rel="canonical" href="https://ornek.com/urun/abc">`,
  },
  'CWV-001': {
    summary: [
      'LCP (Largest Contentful Paint) 2,5 sn altında olmalı.',
      'Hero görseli optimize edin; kritik CSS\'i inline verin.',
      'Sunucu TTFB ve CDN kullanımını iyileştirin.',
    ],
    example: `<img src="hero.webp" width="1200" height="630" fetchpriority="high" loading="eager">`,
  },
  'CWV-002': {
    summary: [
      'INP (Interaction to Next Paint) 200 ms altında hedefleyin.',
      'Uzun JavaScript görevlerini parçalayın.',
      'Üçüncü parti scriptleri erteleyin veya kaldırın.',
    ],
    example: `// Ağır işi requestIdleCallback ile erteleyin
requestIdleCallback(() => initAnalytics());`,
  },
  'CWV-003': {
    summary: [
      'CLS (Cumulative Layout Shift) 0,1 altında olmalı.',
      'Görsel/video için width/height belirtin.',
      'Reklam alanları için sabit yükseklik rezerve edin.',
    ],
    example: `<img src="banner.jpg" width="728" height="90" alt="Banner">`,
  },
  'RANK-002': {
    summary: [
      'Düşük CTR genelde snippet (title/description) zayıflığına işaret eder.',
      'Title\'ı arama niyetine göre güçlendirin.',
      'Zengin sonuç (FAQ, breadcrumb) uygunluğunu kontrol edin.',
    ],
    example: `<!-- Önce -->
<title>Ürünler</title>

<!-- Sonra -->
<title>Organik Zeytinyağı 500ml — Hızlı Kargo | Mağaza</title>`,
  },
  'RANK-003': {
    summary: [
      'Yüksek gösterim + düşük tıklama = snippet veya relevans sorunu.',
      'İlgili sorgu için sayfa içeriğini güçlendirin.',
      'Rakip SERP snippet\'lerini inceleyin.',
    ],
    example: `Search Console → Performans → Sorgu filtresi
→ İlgili sayfanın title/description\'ını sorguya göre güncelleyin`,
  },
  'thin-content': {
    summary: [
      'Sayfa en az 300+ kelime anlamlı içerik sunmalı.',
      'Konuyu derinlemesine ele alın; kullanıcı sorusunu yanıtlayın.',
      'Boş veya şablon sayfaları birleştirin veya zenginleştirin.',
    ],
    example: `<!-- Kısa -->
<p>Hizmetlerimiz hakkında bilgi alın.</p>

<!-- Zengin -->
<h1>SEO Denetimi Hizmeti</h1>
<p>500+ kural ile tam site taraması...</p>
<ul><li>Teknik SEO</li><li>İçerik kalitesi</li></ul>`,
  },
  'sitemap-coverage-gap': {
    summary: [
      'Sitemap\'teki URL\'lerin büyük kısmı taranamıyorsa erişim sorunu olabilir.',
      'robots.txt engellerini ve noindex\'i kontrol edin.',
      'Sitemap\'i güncel ve erişilebilir tutun.',
    ],
    example: `Search Console → Site Haritaları → Gönderilen sitemap
→ Hata/uyarı URL\'lerini düzeltin`,
  },
  'robots-blocks-all': {
    summary: [
      'Disallow: / tüm siteyi tarayıcıdan engeller.',
      'Yalnızca gizlenecek yolları engelleyin.',
      'İndekslemeyi engellemek için noindex kullanın, robots.txt değil.',
    ],
    example: `# Yanlış
User-agent: *
Disallow: /

# Doğru
User-agent: *
Disallow: /admin/
Allow: /`,
  },
  'robots-blocks-css-js': {
    summary: [
      'CSS/JS engeli Google\'ın sayfayı render etmesini zorlaştırır.',
      'Statik asset yollarını (css, js, assets) Disallow listesinden çıkarın.',
      'Engellenen yolları Search Console URL Denetimi ile test edin.',
    ],
    example: `# Yanlış
Disallow: /assets/
Disallow: /*.css$

# Doğru — yalnızca admin/gizli yollar
Disallow: /admin/`,
  },
  'robots-syntax-warning': {
    summary: [
      'robots.txt geçerli User-agent ve Disallow satırları içermeli.',
      'Boş Disallow satırlarından kaçının.',
      'Google Search Console robots.txt test aracını kullanın.',
    ],
    example: `User-agent: *
Disallow: /private/
Allow: /

Sitemap: https://ornek.com/sitemap.xml`,
  },
  'googlebot-blocked': {
    summary: [
      'Googlebot engelleniyorsa site indekslenemez.',
      'CDN/WAF bot koruma kurallarını kontrol edin.',
      'Cloaking (bot\'a farklı yanıt) Google politikasına aykırıdır.',
    ],
    example: `# nginx — Googlebot engelini kaldırın
# if ($http_user_agent ~* Googlebot) { return 403; }  ← kaldırın`,
  },
  'sitemap-xml-invalid': {
    summary: [
      'Sitemap geçerli XML ve urlset/sitemapindex kök öğesi içermeli.',
      'Sitemap URL\'si 200 döndürmeli.',
      'Search Console Site Haritaları bölümünden hataları izleyin.',
    ],
    example: `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url><loc>https://ornek.com/</loc></url>
</urlset>`,
  },
  'sitemap-lastmod-invalid': {
    summary: [
      'lastmod ISO 8601 formatında olmalı (YYYY-MM-DD veya tam tarih-saat).',
      'Gelecek tarih kullanmayın.',
      'Yalnızca gerçekten güncellenen sayfalar için lastmod değiştirin.',
    ],
    example: `<url>
  <loc>https://ornek.com/blog/yazi</loc>
  <lastmod>2025-03-15</lastmod>
</url>`,
  },
  'sitemap-loc-unreachable': {
    summary: [
      'Sitemap\'teki her <loc> erişilebilir olmalı (404/5xx olmamalı).',
      'Kırık URL\'leri düzeltin veya sitemap\'ten kaldırın.',
      'Yönlendirme zincirlerini minimize edin.',
    ],
    example: `# Kırık URL'yi kaldırın veya 301 ile yeni URL'ye yönlendirin
curl -I https://ornek.com/eski-sayfa`,
  },
  'canonical-target-error': {
    summary: [
      'Canonical hedef URL 200 döndürmeli.',
      'Canonical, tercih edilen (indekslenecek) URL\'yi göstermeli.',
      'Zincirleme veya çapraz domain canonical\'lardan kaçının.',
    ],
    example: `<link rel="canonical" href="https://ornek.com/urun/abc">
<!-- hedef URL tarayıcıda 200 açmalı -->`,
  },
  'redirect-temporary-302': {
    summary: [
      'Kalıcı taşınmalarda 301 (Moved Permanently) kullanın.',
      '302/307 geçici yönlendirme sinyali verir; link equity aktarımı gecikebilir.',
      'HTTP → HTTPS ve www birleştirmede 301 tercih edin.',
    ],
    example: `# Apache
Redirect 301 /eski https://ornek.com/yeni`,
  },
  'RICH-002': {
    summary: [
      'FAQPage için mainEntity içinde Question + acceptedAnswer olmalı.',
      'Product için name + offers veya rating/review gerekir.',
      'Article için headline, author ve datePublished zorunludur.',
    ],
    example: `<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "FAQPage",
  "mainEntity": [{
    "@type": "Question",
    "name": "SEO denetimi nedir?",
    "acceptedAnswer": {
      "@type": "Answer",
      "text": "Sitenizi arama motoru kurallarına göre tarayan bir analizdir."
    }
  }]
}
</script>`,
  },
  'SD-FAQ-001': {
    summary: [
      'FAQPage schema mainEntity dizisi Question nesneleri içermeli.',
      'Her soruda name ve acceptedAnswer.text zorunlu.',
      'Google Zengin Sonuçlar Testi ile doğrulayın.',
    ],
    example: `"@type": "FAQPage",
"mainEntity": [{ "@type": "Question", "name": "...", "acceptedAnswer": { "@type": "Answer", "text": "..." } }]`,
  },
  'SD-PROD-001': {
    summary: [
      'Product için name, image ve offers gerekir.',
      'Offer içinde price ve availability ekleyin.',
      'Gerçek stok durumunu availability ile belirtin.',
    ],
    example: `"@type": "Product", "name": "...", "image": "...", "offers": { "@type": "Offer", "price": "99", "availability": "https://schema.org/InStock" }`,
  },
  'js-soft-404': {
    summary: [
      'HTTP 200 dönen ama "404/bulunamadı" içeren sayfalar soft 404 sayılır.',
      'Gerçekten yoksa 404 status kodu kullanın.',
      'Render öncesi/sonrası tutarsızlık da soft 404 işaretidir.',
    ],
    example: `<!-- Yanlış: 200 + "Sayfa bulunamadı" -->
<!-- Doğru: HTTP 404 veya anlamlı 200 içerik -->`,
  },
  'js-cloaking-score': {
    summary: [
      'Bot ve kullanıcıya farklı title/H1/içerik sunmak cloaking sayılır.',
      'SSR ile ilk HTML\'de kritik içeriği sağlayın.',
      'Playwright diff ile farkı minimize edin.',
    ],
    example: `Static HTML ve JS render sonrası aynı <title> ve H1 kullanın.`,
  },
  'SPAM-003': {
    summary: [
      'Birbirine çok benzeyen başlıklı sayfalar doorway riski taşır.',
      'Her sayfa benzersiz değer sunmalı.',
      'Şablon sayfaları birleştirin veya noindex uygulayın.',
    ],
    example: `<!-- 10 sayfa: "SEO Ankara", "SEO İstanbul" ... aynı şablon -->`,
  },
  'EC-003': {
    summary: [
      'Filtre/sıralama URL\'leri genelde indekslenmemeli.',
      'noindex veya canonical ile ana kategori sayfasına yönlendirin.',
      'Google Search Console\'da parametreli URL\'leri izleyin.',
    ],
    example: `<meta name="robots" content="noindex, follow">
<!-- veya canonical ana liste URL'sine -->`,
  },
  'robots-meta-header-conflict': {
    summary: [
      'Meta robots ve X-Robots-Tag aynı sinyali vermeli.',
      'noindex meta + index header çelişkisi Google\'ı karıştırır.',
      'Sunucu başlıkları ve HTML meta\'yı senkronize edin.',
    ],
    example: `<!-- Meta ve header ikisi de noindex VEYA ikisi de index -->`,
  },
  'SC-002': {
    summary: [
      'URL Denetimi geçme oranı düşükse indeks sorunları yaygındır.',
      'Başarısız URL\'lerde noindex, robots, canonical kontrol edin.',
      'Search Console → Sayfa indeksleme raporunu inceleyin.',
    ],
    example: `Search Console → URL Denetimi → Sorunlu URL'leri düzeltin`,
  },
  'SC-004': {
    summary: [
      'Gönderilen sitemap\'te hata varsa URL\'ler indekslenmeyebilir.',
      'Site Haritaları bölümündeki hata detaylarını okuyun.',
      'Kırık URL\'leri düzeltip sitemap\'i yeniden gönderin.',
    ],
    example: `Search Console → Site Haritaları → Hata satırına tıklayın`,
  },
  'INDEX-003': {
    summary: [
      'site: sorgusu ile sayfa indekste görünmüyor olabilir.',
      'Yeni sayfalar indekslenmesi günler sürebilir.',
      'Bağlı modda URL Denetimi kesin sonuç verir.',
    ],
    example: `site:ornek.com/sayfa-yolu`,
  },
  'RANK-004': {
    summary: [
      'Önceki denetime göre pozisyon veya gösterim düşmüş.',
      'İçerik güncellemesi ve snippet iyileştirmesi deneyin.',
      'Rakip SERP değişimlerini kontrol edin.',
    ],
    example: `Search Console → Performans → Sorgu karşılaştırması`,
  },
  'INTL-001': {
    summary: [
      'hreflang alternatifleri karşılıklı referans içermeli.',
      'Sayfa A → B varsa B → A de olmalı.',
      'Google Uluslararası SEO dokümantasyonunu izleyin.',
    ],
    example: `<link rel="alternate" hreflang="tr" href="https://ornek.com/tr/sayfa" />
<link rel="alternate" hreflang="en" href="https://ornek.com/en/page" />`,
  },
  'DISC-001': {
    summary: [
      'Google Discover büyük görselleri tercih eder (≥1200px genişlik).',
      'og:image:width ve og:image:height meta ekleyin.',
      '16:9 veya 4:3 oranlı temsili görsel kullanın.',
    ],
    example: `<meta property="og:image" content="https://ornek.com/og-1200.jpg">
<meta property="og:image:width" content="1200">`,
  },
  'AMP-001': {
    summary: [
      'rel="amphtml" geçerli bir AMP URL\'ye işaret etmeli.',
      'AMP sayfası doğrulama aracından geçmeli.',
      'AMP kullanmıyorsanız amphtml linkini kaldırın.',
    ],
    example: `<link rel="amphtml" href="https://ornek.com/sayfa/amp/">`,
  },
  'SCHED-001': {
    summary: [
      'Zamanlanmış taramada SEO skoru önceki koşa göre belirgin düştü.',
      'Yeni kritik/uyarı sorunlarını önceliklendirin.',
      'Son deploy veya içerik değişikliklerini kontrol edin.',
    ],
    example: `Skor: 82 → 68 (−14)`,
  },
  'SCHED-002': {
    summary: [
      'Önceki taramada olmayan yeni kritik kural ihlali.',
      'İlgili kuralın fix rehberini uygulayın.',
    ],
    example: `Yeni kritik: CAN-001 canonical hedefi`,
  },
  'MIGR-001': {
    summary: [
      'Eski domain kalıcı 301/308 ile yeni siteye yönlenmiyor.',
      'Tüm eski URL\'ler için sunucu yönlendirmesi ekleyin.',
    ],
    example: `https://eski.com/ → 301 → https://yeni.com/`,
  },
  'MIGR-002': {
    summary: [
      'Domain kayıt süresi yakında bitiyor.',
      'Yenilemeyi geciktirmeyin; süresi dolmuş domain indeks kaybına yol açar.',
    ],
    example: `RDAP expiration: 2026-06-15`,
  },
  'ANALYTICS-001': {
    summary: [
      'GA4 oturum veya SC organik tıklama düşüşü.',
      'Teknik indeks sorunları ile trafik verisini birlikte inceleyin.',
    ],
    example: `GA4: 1200 → 780 oturum (−35%)`,
  },
  'SEC-001': {
    summary: [
      'Safe Browsing, HTTPS ve güvenlik sorunları birleşik skoru düşürüyor.',
      'Search Console Güvenlik ve Manuel İşlemler bölümünü kontrol edin.',
    ],
    example: `Güvenlik skoru: 45/100`,
  },
  'MOB-001': {
    summary: [
      'Viewport meta eksik veya hatalı.',
      'Mobil-first indeksleme için viewport zorunludur.',
    ],
    example: `<meta name="viewport" content="width=device-width, initial-scale=1">`,
  },
  'MOB-002': {
    summary: [
      'Dokunma hedefleri mobil için yeterince büyük değil.',
      'Buton/link minimum 48×48px hedef alanı kullanın.',
    ],
    example: `Lighthouse tap-targets audit`,
  },
  'MOB-003': {
    summary: [
      'Mobilde okunabilir font boyutu yetersiz.',
      'Gövde metni için en az 16px kullanın.',
    ],
    example: `Lighthouse font-size audit`,
  },
  'RANK-005': {
    summary: [
      'Takip edilen anahtar kelime Google ilk 10 sonucunda yok.',
      'Custom Search API ile SERP kontrolü yapılır.',
      'İçerik hedeflemesi ve indeks engellerini kontrol edin.',
    ],
    example: `"seo araçları" → pozisyon 10+`,
  },
  'RANK-006': {
    summary: [
      'Takip edilen kelimede SERP pozisyonu önceki denetime göre düştü.',
      'Rakip snippet ve içerik kalitesini karşılaştırın.',
    ],
    example: `Pozisyon 3 → 7`,
  },
  'LINK-EXT-002': {
    summary: [
      'Ahrefs veya Moz API ile harici referring domain sayısı düşük.',
      'API key yapılandırılmamışsa bu kontrol atlanır.',
    ],
    example: `Ahrefs: 3 referring domain`,
  },
  'image-no-srcset': {
    summary: [
      'Büyük görsellerde srcset/sizes eksik; mobilde gereksiz bant genişliği kullanılır.',
      'Farklı genişlikler için srcset ve uygun sizes değeri ekleyin.',
      'PageSpeed "properly size images" uyarısıyla ilişkilidir.',
    ],
    example: `<img src="hero-800.jpg"
  srcset="hero-400.jpg 400w, hero-800.jpg 800w, hero-1200.jpg 1200w"
  sizes="(max-width: 600px) 100vw, 800px"
  alt="Ürün tanıtım görseli">`,
  },
  'description-too-short': {
    summary: [
      'Meta açıklama çok kısa; snippet\'te yeterli bağlam sunmaz.',
      '120–155 karakter aralığında fayda odaklı özet yazın.',
      'Anahtar kelime doldurmadan doğal Türkçe kullanın.',
    ],
    example: `<meta name="description" content="Profesyonel SEO denetimi ile sitenizdeki teknik ve içerik sorunlarını tespit edin. Ücretsiz tarama başlatın.">`,
  },
  'description-too-long': {
    summary: [
      'Meta açıklama ~160 karakteri aşıyor; Google snippet\'te kesebilir.',
      'En önemli mesajı ilk 155 karaktere sığdırın.',
    ],
    example: `<!-- 155 karakter altında tutun -->`,
  },
  'meta-charset-missing': {
    summary: [
      'UTF-8 charset bildirimi eksik; Türkçe karakterler yanlış görünebilir.',
      'head içinde erken konumda meta charset ekleyin.',
    ],
    example: `<meta charset="utf-8">`,
  },
  'favicon-missing': {
    summary: [
      'Favicon link etiketi bulunamadı.',
      'Tarayıcı sekmesi ve arama sonuçları için /favicon.ico veya link rel="icon" ekleyin.',
    ],
    example: `<link rel="icon" href="/favicon.ico" sizes="any">`,
  },
  'mixed-content': {
    summary: [
      'HTTPS sayfada HTTP kaynak (script, görsel, CSS) yükleniyor.',
      'Tarayıcı güvenlik uyarısı verebilir; PageSpeed skorunu düşürür.',
      'Tüm kaynak URL\'lerini https:// veya protokol-relative // kullanmayın — tam HTTPS tercih edin.',
    ],
    example: `<!-- Yanlış -->
<script src="http://example.com/analytics.js"></script>
<!-- Doğru -->
<script src="https://example.com/analytics.js"></script>`,
  },
  'meta-keywords-deprecated': {
    summary: [
      'meta keywords etiketi Google tarafından yıllardır dikkate alınmıyor.',
      'Gereksiz etiket kod kirliliği yaratır.',
      'Title, description ve içerik kalitesine odaklanın.',
    ],
    example: `<!-- Kaldırın -->
<meta name="keywords" content="seo, anahtar kelime">`,
  },
  'og-description-missing': {
    summary: [
      'Open Graph og:description sosyal paylaşım özetini belirler.',
      'Facebook, LinkedIn ve bazı mesajlaşma uygulamaları bu alanı kullanır.',
      'Meta description ile aynı veya paylaşıma özel kısa özet yazın.',
    ],
    example: `<meta property="og:description" content="Profesyonel SEO denetimi ile sitenizdeki sorunları tespit edin.">`,
  },
  'twitter-card-missing': {
    summary: [
      'twitter:card etiketi X/Twitter paylaşım kartı tipini belirler.',
      'summary_large_image büyük görsel önizlemesi sağlar.',
      'og:image ile birlikte tutarlı görsel kullanın.',
    ],
    example: `<meta name="twitter:card" content="summary_large_image">
<meta name="twitter:title" content="Sayfa başlığı">
<meta name="twitter:image" content="https://ornek.com/og-image.jpg">`,
  },
  'js-lazy-content': {
    summary: [
      'JavaScript render sonrası içerik belirgin şekilde artıyor.',
      'Googlebot bazen lazy-loaded içeriği kaçırabilir veya gecikmeli indeksler.',
      'Kritik metin ve linkleri SSR veya statik HTML\'de sağlayın.',
    ],
    example: `<!-- Kritik H1 ve paragraf ilk HTML yanıtında olsun -->
<h1>Ürün Adı</h1>
<p>Ana açıklama metni sunucu tarafında render edilsin.</p>`,
  },
  'keyword-stuffing-title': {
    summary: [
      'Başlıkta aynı kelime 3+ kez tekrarlanıyor.',
      'Google spam politikasına aykırı olabilir.',
      'Doğal, okunabilir tek bir mesaj taşıyan başlık yazın.',
    ],
    example: `<!-- Yanlış -->
<title>SEO SEO SEO Hizmeti Ankara SEO</title>
<!-- Doğru -->
<title>Profesyonel SEO Danışmanlığı — Ankara</title>`,
  },
  'hidden-text-css': {
    summary: [
      'display:none veya font-size:0 ile gizlenmiş metin tespit edildi.',
      'Kullanıcıya görünmeyen anahtar kelime metni spam sayılır.',
      'Gizli metni kaldırın; görünür içerikle aynı bilgiyi sunun.',
    ],
    example: `<!-- Kaldırın -->
<p style="display:none">gizli anahtar kelime doldurma</p>`,
  },
  'SPAM-004': {
    summary: [
      'Şüpheli dış domain iframe veya script bulundu.',
      'Güvenilmeyen kaynaklar site güvenliğini ve SEO\'yu tehdit eder.',
      'Yalnızca güvenilir CDN ve analitik sağlayıcıları kullanın.',
    ],
    example: `<!-- Bilinmeyen üçüncü taraf scriptlerini kaldırın veya SRI ekleyin -->
<script src="https://bilinen-cdn.com/lib.js" integrity="..." crossorigin="anonymous"></script>`,
  },
  'SPAM-005': {
    summary: [
      'Kullanıcı içeriğindeki (UGC) dış linkler nofollow olmadan bırakılmış.',
      'Spam link profili riski oluşur.',
      'Yorum ve forum linklerine rel="nofollow ugc" ekleyin.',
    ],
    example: `<a href="https://dis-site.com" rel="nofollow ugc">Kullanıcı linki</a>`,
  },
  'SPAM-006': {
    summary: [
      'Gövde metninde aşırı kelime tekrarı (keyword stuffing).',
      'Okunabilirliği düşürür ve spam sinyali verir.',
      'Doğal dilde, konu odaklı paragraflar yazın.',
    ],
    example: `Anahtar kelimeyi paragrafta 1–2 kez doğal biçimde kullanın; 10+ tekrarlamayın.`,
  },
  'product-schema-missing': {
    summary: [
      'Ürün sayfasında Product JSON-LD yok.',
      'Google Shopping ve zengin sonuç fırsatı kaçırılır.',
      'name, image, offers (price, availability) alanlarını ekleyin.',
    ],
    example: `<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "Product",
  "name": "Ürün Adı",
  "image": "https://ornek.com/urun.jpg",
  "offers": {
    "@type": "Offer",
    "price": "299.00",
    "priceCurrency": "TRY",
    "availability": "https://schema.org/InStock"
  }
}
</script>`,
  },
  'breadcrumb-missing': {
    summary: [
      'Breadcrumb navigasyonu veya BreadcrumbList schema eksik.',
      'Google arama sonuçlarında breadcrumb gösterimini destekler.',
      'Görünür breadcrumb + JSON-LD BreadcrumbList birlikte kullanın.',
    ],
    example: `<nav aria-label="Breadcrumb">
  <a href="/">Ana Sayfa</a> › <a href="/kategori">Kategori</a> › Ürün
</nav>`,
  },
  'EC-001': {
    summary: [
      'Product Offer schema\'da price veya availability eksik.',
      'Fiyat snippet\'i ve stok bilgisi gösterilemez.',
      'Offer nesnesine geçerli price, priceCurrency ve availability ekleyin.',
    ],
    example: `"offers": {
  "@type": "Offer",
  "price": "149.90",
  "priceCurrency": "TRY",
  "availability": "https://schema.org/InStock"
}`,
  },
  'EC-002': {
    summary: [
      'Product schema\'da review veya aggregateRating yok.',
      'Yıldızlı snippet arama sonuçlarında görünmeyebilir.',
      'Gerçek müşteri yorumlarından AggregateRating oluşturun; sahte puan kullanmayın.',
    ],
    example: `"aggregateRating": {
  "@type": "AggregateRating",
  "ratingValue": "4.6",
  "reviewCount": "128"
}`,
  },
  'EC-004': {
    summary: [
      'Sayfalandırılmış liste sayfalarında rel=next/prev veya canonical eksik.',
      'Google tüm sayfa parametrelerini ayrı URL sanabilir.',
      'rel="next"/"prev" linkleri veya view-all canonical kullanın.',
    ],
    example: `<link rel="prev" href="/urunler?sayfa=1">
<link rel="next" href="/urunler?sayfa=3">
<link rel="canonical" href="/urunler?sayfa=2">`,
  },
  'video-schema-missing': {
    summary: [
      'Sayfada video embed var ama VideoObject schema yok.',
      'Video zengin sonuçları ve video araması için schema gerekir.',
      'thumbnailUrl, name, uploadDate ve embedUrl ekleyin.',
    ],
    example: `<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "VideoObject",
  "name": "Ürün tanıtım videosu",
  "thumbnailUrl": "https://ornek.com/thumb.jpg",
  "uploadDate": "2026-01-15",
  "embedUrl": "https://www.youtube.com/embed/VIDEO_ID"
}
</script>`,
  },
  'url-too-deep': {
    summary: [
      'URL dizin derinliği 4 seviyeden fazla.',
      'Sığ URL yapısı tarama ve kullanıcı deneyimi için daha iyidir.',
      'Gereksiz alt klasörleri birleştirin (/a/b/c/d/e → /kategori/urun).',
    ],
    example: `<!-- Derin -->
/tr/2024/blog/kategori/alt-kategori/makale
<!-- Tercih -->
/blog/makale-basligi`,
  },
  'intrusive-interstitial': {
    summary: [
      'Tam ekran popup veya interstitial tespit edildi.',
      'Google mobil arama sıralamasını olumsuz etkileyebilir.',
      'İçeriğe erişimi engellemeyen banner veya gecikmeli modal kullanın.',
    ],
    example: `Sayfa yüklenirken tam ekran reklam yerine alt banner veya kullanıcı tıklamasıyla açılan modal tercih edin.`,
  },
  'max-image-preview-missing': {
    summary: [
      'max-image-preview:large robots meta yok.',
      'Google Discover ve büyük görsel snippet\'leri sınırlanabilir.',
      'Büyük görsel önizlemesine izin vermek için meta ekleyin.',
    ],
    example: `<meta name="robots" content="index, follow, max-image-preview:large">`,
  },
  'INDEX-002': {
    summary: [
      'Sayfa indekslenmemiş olabilir (site: sorgusu veya SC verisi).',
      'noindex, robots.txt engeli veya düşük kalite nedeni olabilir.',
      'Search Console URL Denetimi ile kesin durumu kontrol edin.',
    ],
    example: `Search Console → URL Denetimi → "Canlı test" → İndeksleme durumu`,
  },
  'LINK-EXT-001': {
    summary: [
      'Dahili link ağı zayıf — az sayfa birbirine bağlanıyor.',
      'Yetim sayfalar ve düşük crawl önceliği oluşur.',
      'Hub sayfalar, footer navigasyon ve bağlamsal iç linkler ekleyin.',
    ],
    example: `Ana kategori sayfasından alt sayfalara, blog yazılarından ilgili ürünlere link verin.`,
  },
  'SC-003': {
    summary: [
      'Search Console\'da manuel işlem veya ciddi indeks engeli olabilir.',
      'Site genelinde görünürlük kaybına yol açar.',
      'Manuel işlemler ve Güvenlik sorunları raporlarını hemen inceleyin.',
    ],
    example: `Search Console → Güvenlik ve Manuel işlemler → Sorunu giderip yeniden inceleme isteyin`,
  },
  'SCHED-003': {
    summary: [
      'Zamanlanmış taramada SEO skoru önceki koşa göre belirgin iyileşti.',
      'Olumlu trend — kritik sorun kalmadığından emin olun.',
      'İyileşmeye neyin yol açtığını not alın (deploy, içerik, teknik düzeltme).',
    ],
    example: `Skor: 68 → 82 (+14)`,
  },
  'INTL-002': {
    summary: [
      'ccTLD (.com.tr, .de) kullanılıyor; çok dilli yapı için strateji gözden geçirilmeli.',
      'Her ülke için ayrı ccTLD yönetimi maliyetlidir.',
      'hreflang, subdomain veya alt dizin stratejisini dokümante edin.',
    ],
    example: `ornek.com (global) + tr.ornek.com veya ornek.com/tr/ + hreflang alternates`,
  },
  'robots-nosnippet-active': {
    summary: [
      'nosnippet direktifi aktif — arama snippet\'i gösterilmez.',
      'AI Özetleri ve zengin snippet\'ler de kısıtlanabilir.',
      'Snippet gösterilmesini istiyorsanız nosnippet\'i kaldırın.',
    ],
    example: `<!-- Snippet istiyorsanız -->
<meta name="robots" content="index, follow">
<!-- nosnippet kaldırıldı -->`,
  },
  'data-nosnippet-invalid': {
    summary: [
      'data-nosnippet yalnızca span, div veya section öğelerinde geçerlidir.',
      'Yanlış öğede kullanım Google tarafından yok sayılır veya hata verir.',
      'Snippet\'ten hariç tutmak istediğiniz bloğu geçerli bir kapsayıcıya alın.',
    ],
    example: `<div data-nosnippet>
  Bu blok arama snippet'inde gösterilmez.
</div>`,
  },
  'x-robots-max-snippet-zero': {
    summary: [
      'X-Robots-Tag max-snippet:0 snippet\'i tamamen gizler.',
      'HTTP yanıt başlığı HTML meta\'dan önceliklidir.',
      'Snippet gösterimi için max-snippet değerini artırın veya kaldırın.',
    ],
    example: `X-Robots-Tag: index, follow, max-snippet:160`,
  },
  'SD-ART-001': {
    summary: [
      'Article schema zorunlu alanları eksik (headline, author, datePublished).',
      'Haber ve blog zengin sonuçları için gerekli.',
      'NewsArticle veya Article tipini doğru alanlarla doldurun.',
    ],
    example: `{
  "@type": "NewsArticle",
  "headline": "Makale başlığı",
  "author": { "@type": "Person", "name": "Yazar Adı" },
  "datePublished": "2026-06-07T10:00:00+03:00"
}`,
  },
  'SD-BREAD-001': {
    summary: [
      'BreadcrumbList schema itemListElement boş veya eksik.',
      'Breadcrumb zengin sonucu için en az iki seviye gerekir.',
      'position, name ve item URL alanlarını sırayla doldurun.',
    ],
    example: `"itemListElement": [
  { "@type": "ListItem", "position": 1, "name": "Ana Sayfa", "item": "https://ornek.com/" },
  { "@type": "ListItem", "position": 2, "name": "Blog", "item": "https://ornek.com/blog" }
]`,
  },
  'SD-LOCAL-001': {
    summary: [
      'LocalBusiness schema\'da address veya telephone eksik.',
      'Yerel arama ve Haritalar entegrasyonu zayıflar.',
      'PostalAddress ve geçerli telefon numarası ekleyin.',
    ],
    example: `"address": {
  "@type": "PostalAddress",
  "streetAddress": "Atatürk Cad. 1",
  "addressLocality": "İstanbul",
  "postalCode": "34000",
  "addressCountry": "TR"
},
"telephone": "+90-212-555-0100"`,
  },
  'SD-VID-001': {
    summary: [
      'VideoObject schema thumbnailUrl veya uploadDate eksik.',
      'Video zengin sonuçları için zorunlu alanlar.',
      'Yüksek çözünürlüklü thumbnail (min 160x90) kullanın.',
    ],
    example: `"thumbnailUrl": "https://ornek.com/video-thumb.jpg",
"uploadDate": "2026-03-01T08:00:00+03:00"`,
  },
  'SD-ORG-001': {
    summary: [
      'Organization schema name veya url eksik.',
      'Marka bilgi paneli ve Knowledge Graph sinyali zayıflar.',
      'Site kökünde Organization JSON-LD ile name, url ve logo ekleyin.',
    ],
    example: `{
  "@type": "Organization",
  "name": "Şirket Adı",
  "url": "https://ornek.com",
  "logo": "https://ornek.com/logo.png"
}`,
  },
  'EEAT-002': {
    summary: [
      'LLM (E-E-A-T) analizi sayfada ek içerik kalitesi bulgusu raporladı.',
      'Yazar kimliği, kaynak gösterimi ve uzmanlık sinyalleri zayıf olabilir.',
      'İnsan odaklı, özgün ve güvenilir içerik üretin; otomatik/spam içerikten kaçının.',
    ],
    example: `<!-- Güven sinyalleri -->
<p>Yazar: <a href="/yazar/ahmet">Ahmet Yılmaz</a>, 10 yıl SEO deneyimi</p>
<p>Kaynak: <a href="https://developers.google.com/search">Google Search Central</a></p>`,
  },
};

export function getRulePlaybook(ruleId: string): RulePlaybookEntry | null {
  return RULE_PLAYBOOK[ruleId] ?? (RULE_CATALOG[ruleId] ? catalogToPlaybook(RULE_CATALOG[ruleId]) : null);
}

export function getRulePlaybookOrFallback(issue: {
  ruleId: string;
  message: string;
  fixHint?: string | null;
  docUrl?: string | null;
}): RulePlaybookEntry | null {
  const pb = RULE_PLAYBOOK[issue.ruleId];
  if (pb) return pb;
  const catalog = RULE_CATALOG[issue.ruleId];
  if (catalog) return catalogToPlaybook(catalog);
  const summary: string[] = [];
  if (issue.fixHint) summary.push(issue.fixHint);
  if (issue.message && !summary.includes(issue.message)) summary.push(issue.message);
  if (summary.length === 0) return null;
  return {
    summary,
    example: issue.docUrl ? `Kaynak: ${issue.docUrl}` : 'Detaylar için Google dokümantasyonuna bakın.',
  };
}
