/** Merchant Center uyumluluk ekranındaki metrik açıklamaları (ⓘ ipuçları). */
export const GMC_METRIC_HINTS = {
  complianceScore:
    'Genel uyumluluk skoru (0–100). Ürün skorlarının ağırlıklı ortalaması (%85) ile site hazırlık skorunun (%15) birleşimidir. ' +
    'Uyumlu ürün 100, kısmen 70, uyumsuz 30 puanla sayılır.',

  compliantCount:
    'Skoru ≥90 olan ürün sayısı. Kritik/uyarı sorunları az; GMC ürün gereksinimlerine büyük ölçüde uygun.',

  partialCount:
    'Skoru 50–89 arası ürün sayısı. Eksik alan veya uyarı var; düzeltmeyle uyumlu (≥90) seviyesine çıkarılabilir.',

  nonCompliantCount:
    'Skoru <50 olan ürün sayısı. Ciddi eksikler veya kritik ihlaller; öncelikli düzeltme gerekir.',

  siteReadiness:
    'Mağaza geneli GMC site gereksinimleri (iade, kargo, iletişim, güvenlik vb.). Her site sorunu −12 puan. ' +
    'Ürün skorundan bağımsızdır; genel uyumluluk skoruna %15 ağırlıkla yansır.',

  productStatus:
    'Ürün bazlı sınıflandırma: Uyumlu ≥90, Kısmen 50–89, Uyumsuz <50. Skor, bulunan sorunların önemine göre 100’den düşülerek hesaplanır.',

  productScore:
    'Ürün skoru 100’den başlar: kritik −25, uyarı −10, bilgi −3. Aynı üründeki tüm sorunlar toplanır.',
} as const;
