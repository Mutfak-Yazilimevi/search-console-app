export const CATEGORY_LABELS: Record<string, string> = {
  'meta-tags': 'Meta etiketler',
  security: 'Güvenlik',
  mobile: 'Mobil',
  content: 'İçerik',
  links: 'Bağlantılar',
  technical: 'Teknik',
  javascript: 'JavaScript SEO',
  'structured-data': 'Yapılandırılmış veri',
  social: 'Sosyal / keşfedilebilirlik',
  discover: 'Keşfet / sosyal',
  'x-robots-tag': 'X-Robots-Tag',
  canonical: 'Canonical',
  image: 'Görseller',
  robots: 'Robots.txt',
  sitemap: 'Site haritası',
  international: 'Uluslararası',
  'geo-ai': 'GEO / AI arama',
  'search-console': 'Search Console',
  'index-status': 'İndeks durumu',
  ranking: 'Sıralama',
  'content-quality': 'İçerik kalitesi',
  'core-web-vitals': 'Core Web Vitals',
  'spam-signals': 'Spam sinyalleri',
  'page-experience': 'Sayfa deneyimi',
  ecommerce: 'E-ticaret',
  video: 'Video',
  backlinks: 'Backlink / link ağı',
  monitoring: 'İzleme / trend',
  migration: 'Site taşıma',
  analytics: 'Analytics',
  'mobile-friendly': 'Mobil uyumluluk',
};

export const SEVERITY_LABELS: Record<string, string> = {
  Critical: 'Kritik',
  Warning: 'Uyarı',
  Info: 'Bilgi',
};

export const AUDIT_STATUS_LABELS: Record<string, string> = {
  Pending: 'Bekliyor',
  Crawling: 'Taranıyor',
  Analyzing: 'Analiz ediliyor',
  Completed: 'Tamamlandı',
  Failed: 'Başarısız',
  Cancelled: 'Durduruldu',
};

export function categoryLabel(cat: string): string {
  return CATEGORY_LABELS[cat] ?? cat;
}

export function severityLabel(severity: string): string {
  return SEVERITY_LABELS[severity] ?? severity;
}

export function auditStatusLabel(status: string | undefined): string {
  if (!status) return '';
  return AUDIT_STATUS_LABELS[status] ?? status;
}
