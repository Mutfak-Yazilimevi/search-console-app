export interface MissingAltImage {
  order: number;
  src: string;
  problem: "missing" | "empty";
  suggestion: string;
}

const MAX_LISTED = 40;

function extractSrcFromImgTag(tag: string): string {
  const src =
    tag.match(/\bsrc=["']([^"']+)["']/i)?.[1]
    ?? tag.match(/\bdata-src=["']([^"']+)["']/i)?.[1]
    ?? tag.match(/\bdata-lazy-src=["']([^"']+)["']/i)?.[1]
    ?? tag.match(/\bsrcset=["']([^"'\s,]+)/i)?.[1];
  return src?.trim() || "(src belirtilmemiş)";
}

function suggestAltFromSrc(src: string): string {
  if (src === "(src belirtilmemiş)") {
    return "Görselin ne gösterdiğini 5–12 kelimeyle yazın.";
  }
  try {
    const file = src.split("/").pop()?.split("?")[0] ?? "";
    const base = decodeURIComponent(file.replace(/\.[a-z0-9]+$/i, ""))
      .replace(/[-_+.]+/g, " ")
      .trim();
    if (base.length >= 3 && !/^(img|image|photo|pic|banner|logo)\d*$/i.test(base)) {
      return `Örn: "${base}" — görseli kısaca tarif edin (anahtar kelime doldurmayın).`;
    }
  } catch {
    // ignore decode errors
  }
  return "Görselin konusunu kısaca tarif edin; dekoratifse alt=\"\" kullanın.";
}

export function extractImagesMissingAlt(html: string): MissingAltImage[] {
  const results: MissingAltImage[] = [];
  const regex = /<img\b[^>]*>/gi;
  let match: RegExpExecArray | null;
  let order = 0;

  while ((match = regex.exec(html)) !== null) {
    const tag = match[0];
    const altMatch = tag.match(/\balt=["']([^"']*)["']/i);
    const altValue = altMatch ? altMatch[1].trim() : null;
    if (altValue !== null && altValue.length > 0) continue;

    order++;
    const src = extractSrcFromImgTag(tag);
    results.push({
      order,
      src,
      problem: altMatch ? "empty" : "missing",
      suggestion: suggestAltFromSrc(src),
    });
  }

  return results;
}

export function buildImgAltMissingEvidence(html: string): string {
  const all = extractImagesMissingAlt(html);
  const listed = all.slice(0, MAX_LISTED);
  return JSON.stringify({
    type: "img-alt-missing",
    count: all.length,
    truncated: all.length > MAX_LISTED,
    images: listed,
  });
}
