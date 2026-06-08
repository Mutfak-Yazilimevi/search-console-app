/** SEO denetim ekranındaki panel ve metrik açıklamaları (ⓘ ipuçları). */
export const AUDIT_HINTS = {
  connectedMode:
    'Search Console hesabınız bağlıyken ek veri çeker: performans, indeks kapsamı, anahtar kelimeler ve SERP takibi.',

  dashboardScore: 'Son tamamlanan bağlı taramadaki SEO skoru (0–100). Yüksek = daha az sorun.',
  dashboardCritical: 'Kritik önemdeki sorun sayısı — arama görünürlüğünü doğrudan etkileyebilir.',
  dashboardWarning: 'Uyarı düzeyindeki sorun sayısı — düzeltilmesi önerilir.',
  dashboardDelta: 'Bir önceki taramaya göre skor değişimi. Negatif = kötüleşme.',

  seoScore:
    'Benzersiz kural ihlallerine göre hesaplanır: her kritik −15, uyarı −6, bilgi −2 (en fazla 20 bilgi kuralı). ' +
    'Aynı kural birden fazla sayfada olsa bir kez sayılır. Skor 0 olabilir; kritik sayısı 0 olsa bile uyarılar düşürebilir.',
  critical: 'Kritik sorunlar — indeksleme, güvenlik veya temel SEO hataları.',
  criticalReport:
    'Yalnızca kritik düzeyindeki bulguların özeti. Önceki taramaya göre yeni kritik kurallar "Yeni" ile işaretlenir.',
  warning: 'Uyarılar — kullanıcı deneyimi ve sıralama potansiyelini etkileyebilir.',
  info: 'Bilgi düzeyindeki bulgular — iyileştirme fırsatları.',
  pagesCrawled: 'Tarama sırasında ziyaret edilen benzersiz sayfa sayısı (robots ve derinlik limitine tabi).',

  scPerformance28:
    'Google Search Console’daki son 28 günlük arama performansı — yalnızca bağlı modda.',
  clicks: 'Arama sonuçlarından sitenize yapılan tıklama sayısı.',
  impressions: 'Arama sonuçlarında sitenizin görüntülenme sayısı.',
  ctr: 'Tıklama oranı (Tıklama ÷ Gösterim). Yüksek CTR genelde daha iyi snippet ve pozisyon demektir.',
  position: 'Ortalama arama sonucu sırası. 1 = en üst; değer küçüldükçe sıralama iyileşir.',

  scCoverage:
    'Search Console URL Denetimi ve site haritası verilerinden indeks durumu özeti.',
  indexed: 'Google’ın indekslediği veya sorunsuz bulduğu URL sayısı.',
  excluded: 'İndekslenmemiş, hariç tutulan veya sorunlu URL sayısı.',
  inspected: 'Search Console üzerinden denetlenen URL sayısı.',
  sitemapPath: 'Search Console’a bildirilen site haritası dosyası.',
  sitemapErrors: 'Site haritasındaki hata sayısı — Google URL’leri işleyemiyor olabilir.',
  sitemapWarnings: 'Site haritası uyarıları — genelde düşük öncelikli sorunlar.',

  serpKeywords:
    'Takip listesine eklediğiniz anahtar kelimeler için Google arama sonuçlarındaki tahmini sıralama.',
  serpPosition: 'Sitenizin bu kelime için ilk 10 sonuçtaki sırası. 10+ = ilk sayfada değil.',
  serpMatchedUrl: 'Bu kelime için Google’ın sıraladığı sayfa URL’si.',

  scKeywords:
    'Search Console’un raporladığı en çok tıklama/gösterim alan arama sorguları (bağlı mod).',

  contentQuality:
    'E-E-A-T (Deneyim, Uzmanlık, Otorite, Güven) kontrol listesine göre içerik kalite skoru.',
  eeatScore: '0–100 arası içerik kalite puanı. Düşük skor = eksik güven/uzmanlık sinyalleri.',

  pageSpeed:
    'Google PageSpeed Insights ile ölçülen performans ve Core Web Vitals (mobil).',
  performanceScore: 'Lighthouse performans skoru (0–100). 50 altı genelde yavaş sayfa demektir.',
  lcp: 'Largest Contentful Paint — ana içeriğin yüklenme süresi. İyi: ≤2,5 sn.',
  inp: 'Interaction to Next Paint — etkileşim gecikmesi. İyi: ≤200 ms.',
  cls: 'Cumulative Layout Shift — görsel kayma. İyi: ≤0,1.',

  indexStatus:
    'Google Custom Search veya Search Console ile tahmini indeks kapsamı.',
  estimatedIndexed: 'Google’da bulunduğu tahmin edilen sayfa sayısı.',
  crawledPages: 'Bu taramada ziyaret edilen sayfa sayısı.',
  coverageRatio: 'Taranan sayfaların tahmini indekslenme oranı.',

  linkProfile:
    'Sitenizin dahili bağlantı ağının özeti — sayfalar birbirine ne kadar iyi bağlanıyor?',
  internalLinks:
    'Tarama sırasında bulunan toplam dahili (site içi) bağlantı sayısı. Güçlü site mimarisi daha fazla anlamlı iç link demektir.',
  targetPages:
    'Dahili linklerle hedef alınan benzersiz sayfa sayısı. Taranan ve link grafiğine dahil edilen URL’ler.',
  orphanPages:
    'Ana sayfa dışında hiçbir dahili linkle ulaşılamayan sayfalar (yetim sayfa). Google ve kullanıcılar bu sayfaları keşfetmekte zorlanır.',
  externalDomains:
    'Sitenize dışarıdan link veren benzersiz domain sayısı (Ahrefs veya Moz kaynağı).',

  issueGroupBy: 'Bulunan sorunları mesaj, kategori, sayfa veya önem düzeyine göre gruplar.',
  issueCategoryFilter: 'Yalnızca seçilen SEO kategorisindeki sorunları gösterir.',
  issueSeverityFilter: 'Önem düzeyine göre filtreler — kritik raporu hızlıca incelemek için "Kritik" seçin.',
} as const;
