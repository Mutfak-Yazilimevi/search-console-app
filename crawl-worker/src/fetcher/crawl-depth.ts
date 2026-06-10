export function normalizeCrawlUrl(url: string): string {
  return url.replace(/\/$/, "") || url;
}

/** URL yol segmenti sayısı — /blog/yazi → 2, kök → 0 */
export function urlPathDepth(url: string): number {
  try {
    return new URL(url).pathname.split("/").filter(Boolean).length;
  } catch {
    return 0;
  }
}

export interface CrawlQueueEntry {
  url: string;
  depth: number;
}

/**
 * En düşük derinlik kazanır: link takibi (BFS) sitemap tahminini geçersiz kılabilir.
 * Zaten taranan URL'ler yeniden kuyruğa alınmaz.
 */
export function scheduleCrawlUrl(
  queue: CrawlQueueEntry[],
  queuedDepth: Map<string, number>,
  crawled: Set<string>,
  url: string,
  depth: number,
): void {
  const normalized = normalizeCrawlUrl(url);
  if (crawled.has(normalized)) return;

  const existing = queuedDepth.get(normalized);
  if (existing !== undefined && existing <= depth) return;

  queuedDepth.set(normalized, depth);
  queue.push({ url: normalized, depth });
}

/** Kuyruktan çıkan gecikmiş (daha derin) kayıtları atla. */
export function shouldSkipQueuedEntry(
  normalized: string,
  depth: number,
  queuedDepth: Map<string, number>,
  crawled: Set<string>,
): boolean {
  if (crawled.has(normalized)) return true;
  const planned = queuedDepth.get(normalized);
  return planned !== undefined && depth > planned;
}
