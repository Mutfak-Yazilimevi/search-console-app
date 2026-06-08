export interface H1Recommendation {
  order: number;
  text: string;
  keepAs: "h1" | "h2" | "h3";
  reason: string;
}

export function extractH1Texts(html: string): string[] {
  const headings: string[] = [];
  const regex = /<h1\b[^>]*>([\s\S]*?)<\/h1>/gi;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(html)) !== null) {
    const text = match[1].replace(/<[^>]+>/g, " ").replace(/\s+/g, " ").trim();
    headings.push(text || "(boş H1)");
  }
  return headings;
}

function normalizeText(value: string): string {
  return value.toLowerCase().replace(/\s+/g, " ").trim();
}

function pickPrimaryH1Index(headings: string[], pageTitle: string | null): number {
  if (headings.length === 0) return 0;

  const titleNorm = pageTitle ? normalizeText(pageTitle) : "";
  if (titleNorm) {
    let bestIdx = 0;
    let bestScore = -1;
    headings.forEach((heading, index) => {
      const hNorm = normalizeText(heading);
      if (hNorm === titleNorm) {
        bestIdx = index;
        bestScore = 1000;
        return;
      }
      if (hNorm.includes(titleNorm) || titleNorm.includes(hNorm)) {
        const score = Math.min(hNorm.length, titleNorm.length);
        if (score > bestScore) {
          bestScore = score;
          bestIdx = index;
        }
      }
    });
    if (bestScore > 0) return bestIdx;
  }

  // En uzun anlamlı H1 genelde hero/main başlığıdır
  let longestIdx = 0;
  let longestLen = 0;
  headings.forEach((heading, index) => {
    if (heading === "(boş H1)") return;
    if (heading.length > longestLen) {
      longestLen = heading.length;
      longestIdx = index;
    }
  });
  return longestIdx;
}

export function buildH1Recommendations(
  headings: string[],
  pageTitle: string | null,
): H1Recommendation[] {
  const primaryIdx = pickPrimaryH1Index(headings, pageTitle);
  const duplicateCounts = new Map<string, number>();
  for (const h of headings) {
    duplicateCounts.set(h, (duplicateCounts.get(h) ?? 0) + 1);
  }

  return headings.map((text, index) => {
    if (index === primaryIdx) {
      return {
        order: index + 1,
        text,
        keepAs: "h1" as const,
        reason: pageTitle
          ? "Sayfa ana başlığı — `<title>` ile en uyumlu veya en kapsamlı H1"
          : "Sayfa ana başlığı — tek H1 olarak bunu bırakın",
      };
    }

    const isDuplicate = (duplicateCounts.get(text) ?? 0) > 1;
    const isShort = text.length <= 24;

    if (isDuplicate || isShort) {
      return {
        order: index + 1,
        text,
        keepAs: "h3" as const,
        reason: isDuplicate
          ? "Tekrarlayan/kısa bileşen başlığı — kart, menü veya widget için H3 (veya `<p class=\"title\">`)"
          : "Kısa alt başlık — navigasyon/bileşen; H3 veya stil sınıfı kullanın",
      };
    }

    return {
      order: index + 1,
      text,
      keepAs: "h2" as const,
      reason: "İçerik bölümü başlığı — ana H1 altında H2 kullanın",
    };
  });
}

export function buildH1MultipleEvidence(html: string, pageTitle: string | null): string {
  const headings = extractH1Texts(html);
  const recommendations = buildH1Recommendations(headings, pageTitle);
  return JSON.stringify({
    type: "h1-multiple",
    count: headings.length,
    pageTitle: pageTitle ?? null,
    headings: recommendations,
  });
}
