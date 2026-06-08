export function extractTitle(html: string): string | null {
  const match = html.match(/<title[^>]*>([^<]*)<\/title>/i);
  return match?.[1]?.trim() || null;
}

export function extractMetaDescription(html: string): string | null {
  const match = html.match(/<meta[^>]+name=["']description["'][^>]+content=["']([^"']*)["']/i)
    ?? html.match(/<meta[^>]+content=["']([^"']*)["'][^>]+name=["']description["']/i);
  return match?.[1]?.trim() || null;
}

export function extractCanonical(html: string): string | null {
  const match = html.match(/<link[^>]+rel=["']canonical["'][^>]+href=["']([^"']+)["']/i)
    ?? html.match(/<link[^>]+href=["']([^"']+)["'][^>]+rel=["']canonical["']/i);
  return match?.[1]?.trim() || null;
}

export function countH1(html: string): number {
  return (html.match(/<h1[\s>]/gi) ?? []).length;
}

export function hasNoindex(html: string): boolean {
  return /<meta[^>]+name=["']robots["'][^>]+content=["'][^"']*noindex/i.test(html)
    || /<meta[^>]+content=["'][^"']*noindex[^"']*["'][^>]+name=["']robots["']/i.test(html);
}

export function hasMetaDescription(html: string): boolean {
  return /<meta[^>]+name=["']description["'][^>]*>/i.test(html);
}

export function hasViewport(html: string): boolean {
  return /<meta[^>]+name=["']viewport["'][^>]*>/i.test(html);
}

export function extractHtmlLang(html: string): string | null {
  const match = html.match(/<html[^>]+lang=["']([^"']+)["']/i);
  return match?.[1]?.trim() || null;
}

export function countImagesWithoutAlt(html: string): number {
  const imgs = html.match(/<img\b[^>]*>/gi) ?? [];
  return imgs.filter((tag) => !/\balt=["'][^"']+["']/i.test(tag)).length;
}

export function extractJsonLdBlocks(html: string): string[] {
  const blocks: string[] = [];
  const regex = /<script[^>]+type=["']application\/ld\+json["'][^>]*>([\s\S]*?)<\/script>/gi;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(html)) !== null) {
    blocks.push(match[1].trim());
  }
  return blocks;
}

export function extractHreflang(html: string): string[] {
  return extractHreflangLinks(html).map((l) => l.lang);
}

export function extractHreflangLinks(html: string): { lang: string; href: string }[] {
  const links: { lang: string; href: string }[] = [];
  const patterns = [
    /<link[^>]+rel=["']alternate["'][^>]+hreflang=["']([^"']+)["'][^>]+href=["']([^"']+)["']/gi,
    /<link[^>]+hreflang=["']([^"']+)["'][^>]+href=["']([^"']+)["'][^>]+rel=["']alternate["']/gi,
    /<link[^>]+href=["']([^"']+)["'][^>]+hreflang=["']([^"']+)["'][^>]+rel=["']alternate["']/gi,
  ];
  for (const regex of patterns) {
    let match: RegExpExecArray | null;
    while ((match = regex.exec(html)) !== null) {
      const a = match[1].trim();
      const b = match[2].trim();
      if (/^https?:\/\//i.test(a)) {
        links.push({ lang: b.toLowerCase(), href: a });
      } else {
        links.push({ lang: a.toLowerCase(), href: b });
      }
    }
  }
  const seen = new Set<string>();
  return links.filter((l) => {
    const key = `${l.lang}|${l.href}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}

export function extractAmpHtmlUrl(html: string): string | null {
  const match = html.match(/<link[^>]+rel=["']amphtml["'][^>]+href=["']([^"']+)["']/i)
    ?? html.match(/<link[^>]+href=["']([^"']+)["'][^>]+rel=["']amphtml["']/i);
  return match?.[1]?.trim() || null;
}

export function extractOgImageWidth(html: string): number | null {
  const w = html.match(/<meta[^>]+property=["']og:image:width["'][^>]+content=["'](\d+)["']/i)
    ?? html.match(/<meta[^>]+content=["'](\d+)["'][^>]+property=["']og:image:width["']/i);
  if (!w) return null;
  const n = parseInt(w[1], 10);
  return Number.isNaN(n) ? null : n;
}

export function extractOgMeta(html: string, property: string): string | null {
  const regex = new RegExp(
    `<meta[^>]+property=["']${property}["'][^>]+content=["']([^"']+)["']`,
    "i",
  );
  const match = html.match(regex)
    ?? html.match(new RegExp(`<meta[^>]+content=["']([^"']+)["'][^>]+property=["']${property}["']`, "i"));
  return match?.[1]?.trim() || null;
}

export function countBadLinks(html: string): { javascript: number; hashRoute: number } {
  let javascript = 0;
  let hashRoute = 0;
  const regex = /<a\b[^>]*\bhref=["']([^"']*)["']/gi;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(html)) !== null) {
    const href = match[1].trim();
    if (/^javascript:/i.test(href)) javascript++;
    if (/^#\//.test(href)) hashRoute++;
  }
  return { javascript, hashRoute };
}

export function extractBodyText(html: string): string {
  const bodyMatch = html.match(/<body[^>]*>([\s\S]*)<\/body>/i);
  return (bodyMatch?.[1] ?? html)
    .replace(/<script[\s\S]*?<\/script>/gi, "")
    .replace(/<style[\s\S]*?<\/style>/gi, "")
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

export function extractFirstParagraphText(html: string): string {
  const firstP = html.match(/<p[^>]*>([\s\S]*?)<\/p>/i);
  return (firstP?.[1] ?? "").replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim();
}
