export function priceBenchmarkStatusLabel(status: string | undefined): string {
  switch (status) {
    case 'Pending': return 'Bekliyor';
    case 'Crawling': return 'Taranıyor';
    case 'Comparing': return 'Karşılaştırılıyor';
    case 'Completed': return 'Tamamlandı';
    case 'Failed': return 'Başarısız';
    case 'Cancelled': return 'İptal edildi';
    default: return status ?? '—';
  }
}

export function marketPositionLabel(position: string | undefined): string {
  switch (position?.toLowerCase()) {
    case 'below': return 'Piyasanın altında';
    case 'average': return 'Ortalama seviye';
    case 'above': return 'Piyasanın üstünde';
    default: return 'Bilinmiyor';
  }
}

export function marketPositionClass(position: string | undefined): string {
  switch (position?.toLowerCase()) {
    case 'below': return 'pos-below';
    case 'average': return 'pos-average';
    case 'above': return 'pos-above';
    default: return 'pos-unknown';
  }
}

export function isPriceBenchmarkRunning(status: string | undefined): boolean {
  return status === 'Pending' || status === 'Crawling' || status === 'Comparing';
}

export function isDiscoverPhase(status: string | undefined, progressPhase?: string | null): boolean {
  return status === 'Crawling' || progressPhase === 'discovering';
}

export function isComparePhase(status: string | undefined, progressPhase?: string | null): boolean {
  return status === 'Comparing' || progressPhase === 'comparing' || progressPhase === 'discover-complete';
}

export function isProductCompared(item: {
  marketOfferCount: number;
  shoppingError?: string | null;
  deltaPercent?: number | null;
  weightedAvgMarketPrice?: number | null;
}): boolean {
  return item.marketOfferCount > 0
    || !!item.shoppingError
    || item.deltaPercent != null
    || item.weightedAvgMarketPrice != null;
}
