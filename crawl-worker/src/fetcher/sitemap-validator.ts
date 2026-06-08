import type { RobotsTxt } from "./robots-sitemap.js";

export interface SitemapValidationResult {
  xmlValid: boolean;
  invalidLastModCount: number;
  unreachableLocCount: number;
  sampledLocs: number;
  detail: string;
}

async function fetchSitemapXml(url: string): Promise<string | null> {
  try {
    const response = await fetch(url, {
      headers: { "User-Agent": "SearchConsoleApp-CrawlWorker/1.0" },
    });
    if (!response.ok) return null;
    return await response.text();
  } catch {
    return null;
  }
}

function resolveSitemapUrl(origin: string, robots: RobotsTxt): string | null {
  if (robots.sitemaps.length > 0) return robots.sitemaps[0];
  return `${origin}/sitemap.xml`;
}

function parseLocsAndLastmods(xml: string): { locs: string[]; lastmods: string[] } {
  const locs = [...xml.matchAll(/<loc>\s*(.*?)\s*<\/loc>/gi)].map((m) => m[1].trim());
  const lastmods = [...xml.matchAll(/<lastmod>\s*(.*?)\s*<\/lastmod>/gi)].map((m) => m[1].trim());
  return { locs, lastmods };
}

function isInvalidLastMod(value: string): boolean {
  const d = Date.parse(value);
  if (Number.isNaN(d)) return true;
  return d > Date.now() + 86_400_000;
}

async function headStatus(url: string): Promise<number> {
  try {
    const response = await fetch(url, {
      method: "HEAD",
      headers: { "User-Agent": "SearchConsoleApp-CrawlWorker/1.0" },
      redirect: "follow",
    });
    return response.status;
  } catch {
    return 0;
  }
}

export async function validatePrimarySitemap(
  origin: string,
  robots: RobotsTxt,
  sampleSize = 10,
): Promise<SitemapValidationResult> {
  const sitemapUrl = resolveSitemapUrl(origin, robots);
  if (!sitemapUrl) {
    return {
      xmlValid: false,
      invalidLastModCount: 0,
      unreachableLocCount: 0,
      sampledLocs: 0,
      detail: "Sitemap URL bulunamadı",
    };
  }

  const xml = await fetchSitemapXml(sitemapUrl);
  if (!xml || !xml.includes("<")) {
    return {
      xmlValid: false,
      invalidLastModCount: 0,
      unreachableLocCount: 0,
      sampledLocs: 0,
      detail: sitemapUrl,
    };
  }

  const hasUrlset = /<urlset/i.test(xml) || /<sitemapindex/i.test(xml);
  if (!hasUrlset) {
    return {
      xmlValid: false,
      invalidLastModCount: 0,
      unreachableLocCount: 0,
      sampledLocs: 0,
      detail: "urlset/sitemapindex kök öğesi yok",
    };
  }

  let { locs, lastmods } = parseLocsAndLastmods(xml);

  if (/<sitemapindex/i.test(xml) && locs.length > 0) {
    const childXml = await fetchSitemapXml(locs[0]);
    if (childXml) {
      const child = parseLocsAndLastmods(childXml);
      locs = child.locs;
      lastmods = child.lastmods;
    }
  }

  const invalidLastModCount = lastmods.filter(isInvalidLastMod).length;
  const sample = locs.slice(0, sampleSize);
  let unreachableLocCount = 0;
  for (const loc of sample) {
    const status = await headStatus(loc);
    if (status >= 400 || status === 0) unreachableLocCount++;
  }

  return {
    xmlValid: true,
    invalidLastModCount,
    unreachableLocCount,
    sampledLocs: sample.length,
    detail: sitemapUrl,
  };
}
