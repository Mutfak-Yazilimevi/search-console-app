export interface RobotsTxt {
  disallowed: string[];
  sitemaps: string[];
  rawText: string;
}

export async function fetchRobotsTxt(origin: string): Promise<RobotsTxt> {
  const result: RobotsTxt = { disallowed: [], sitemaps: [], rawText: "" };

  try {
    const response = await fetch(`${origin}/robots.txt`, {
      headers: { "User-Agent": "SearchConsoleApp-CrawlWorker/1.0" },
    });
    if (!response.ok) return result;

    const text = await response.text();
    result.rawText = text;
    let appliesToAll = false;

    for (const line of text.split("\n")) {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith("#")) continue;

      const [key, ...rest] = trimmed.split(":");
      const value = rest.join(":").trim();

      if (/^user-agent$/i.test(key)) {
        appliesToAll = value === "*" || /googlebot/i.test(value);
        continue;
      }

      if (!appliesToAll) continue;

      if (/^disallow$/i.test(key) && value) result.disallowed.push(value);
      if (/^sitemap$/i.test(key) && value) result.sitemaps.push(value);
    }
  } catch {
    // robots.txt optional
  }

  return result;
}

export function isDisallowed(url: string, robots: RobotsTxt): boolean {
  const path = new URL(url).pathname;
  return robots.disallowed.some((rule) => {
    if (rule === "/") return true;
    return path.startsWith(rule);
  });
}

export async function fetchSitemapUrls(sitemapUrl: string, maxUrls: number): Promise<string[]> {
  const urls: string[] = [];

  try {
    const response = await fetch(sitemapUrl, {
      headers: { "User-Agent": "SearchConsoleApp-CrawlWorker/1.0" },
    });
    if (!response.ok) return urls;

    const xml = await response.text();

    // sitemap index
    const childSitemaps = [...xml.matchAll(/<loc>\s*(.*?)\s*<\/loc>/gi)].map((m) => m[1]);
    if (xml.includes("<sitemapindex") && childSitemaps.length > 0) {
      for (const child of childSitemaps.slice(0, 5)) {
        const childUrls = await fetchSitemapUrls(child, maxUrls - urls.length);
        urls.push(...childUrls);
        if (urls.length >= maxUrls) break;
      }
      return urls.slice(0, maxUrls);
    }

    for (const match of xml.matchAll(/<loc>\s*(.*?)\s*<\/loc>/gi)) {
      urls.push(match[1].trim());
      if (urls.length >= maxUrls) break;
    }
  } catch {
    // sitemap optional
  }

  return urls;
}

export async function discoverStartUrls(origin: string, robots: RobotsTxt, maxPages: number): Promise<string[]> {
  const urls = new Set<string>([origin]);

  for (const sitemap of robots.sitemaps) {
    const found = await fetchSitemapUrls(sitemap, maxPages);
    found.forEach((u) => urls.add(u.replace(/\/$/, "") || u));
  }

  if (robots.sitemaps.length === 0) {
    for (const path of ["/sitemap.xml", "/sitemap_index.xml"]) {
      const found = await fetchSitemapUrls(`${origin}${path}`, maxPages);
      if (found.length > 0) {
        found.forEach((u) => urls.add(u.replace(/\/$/, "") || u));
        break;
      }
    }
  }

  return [...urls].slice(0, maxPages);
}
