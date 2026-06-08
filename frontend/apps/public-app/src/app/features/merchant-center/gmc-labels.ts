export function integrationStatusLabel(status: string): string {
  switch (status) {
    case 'configured': return 'hazır';
    case 'missing': return 'eksik';
    case 'disabled': return 'kapalı';
    default: return status;
  }
}

export function formatComplianceDelta(delta: number): string {
  return delta > 0 ? `+${delta}` : `${delta}`;
}

export function formatCtr(ctr?: number | null): string {
  if (ctr == null) return '—';
  return `${(ctr * 100).toFixed(2)}%`;
}

export function comparisonBannerClass(delta: number): string {
  if (delta >= 5) return 'banner-good';
  if (delta <= -5) return 'banner-bad';
  return 'banner-info';
}

export function isComplianceRunActive(status: string, progressPhase?: string | null): boolean {
  if (progressPhase === 'rescanning') return true;
  return status === 'Pending' || status === 'Crawling' || status === 'Analyzing';
}
