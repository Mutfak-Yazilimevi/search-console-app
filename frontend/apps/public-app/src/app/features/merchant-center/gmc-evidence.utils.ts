export interface ProductEvidenceEntry {
  title?: string;
  url: string;
  meta?: string;
}

export interface ParsedIssueEvidence {
  title?: string;
  descriptionPreview?: string;
  urls: string[];
  products: ProductEvidenceEntry[];
  schemaPrice?: string;
  visiblePrice?: string;
  schemaListPrice?: string;
  rawLines: string[];
}

function pushProduct(result: ParsedIssueEvidence, entry: ProductEvidenceEntry): void {
  result.products.push(entry);
  if (!result.urls.includes(entry.url)) {
    result.urls.push(entry.url);
  }
}

export function parseIssueEvidence(
  ruleId: string,
  evidence: string | null | undefined,
): ParsedIssueEvidence {
  const result: ParsedIssueEvidence = { urls: [], products: [], rawLines: [] };
  if (!evidence?.trim()) return result;

  for (const raw of evidence.split('\n')) {
    const line = raw.trim();
    if (!line) continue;
    result.rawLines.push(line);

    const titleMatch = line.match(/^Başlık:\s*"(.+)"\s*$/);
    if (titleMatch) {
      result.title = titleMatch[1];
      continue;
    }

    const descMatch = line.match(/^Açıklama:\s*"(.+)"\s*$/);
    if (descMatch) {
      result.descriptionPreview = descMatch[1];
      continue;
    }

    const schemaMatch = line.match(/^Schema \(JSON-LD Offer\.price\):\s*(.+)$/i)
      ?? line.match(/^Schema:\s*(.+)$/i)
      ?? line.match(/^schema=([\d.,]+)/i);
    if (schemaMatch) {
      result.schemaPrice = schemaMatch[1].trim();
      continue;
    }

    const visibleMatch = line.match(/^Sayfada görünen fiyat:\s*(.+)$/i)
      ?? line.match(/^Görünen:\s*(.+)$/i)
      ?? line.match(/visible=([\d.,]+)/i);
    if (visibleMatch) {
      result.visiblePrice = visibleMatch[1].trim();
      continue;
    }

    const listMatch = line.match(/^Schema liste fiyatı:\s*(.+)$/i);
    if (listMatch) {
      result.schemaListPrice = listMatch[1].trim();
      continue;
    }

    const productMatch = line.match(/^(.+?) — (https?:\/\/.+)$/);
    if (productMatch) {
      pushProduct(result, { title: productMatch[1].trim(), url: productMatch[2].trim() });
      continue;
    }

    const urlWithMeta = line.match(/^(https?:\/\/.+?) \((.+)\)$/);
    if (urlWithMeta) {
      pushProduct(result, { url: urlWithMeta[1].trim(), meta: urlWithMeta[2].trim() });
      continue;
    }

    if (/^https?:\/\//i.test(line)) {
      pushProduct(result, { url: line });
      continue;
    }

    if (ruleId === 'GMC-X-001' && !result.title && result.products.length === 0) {
      result.title = line;
    }
  }

  if (!result.schemaPrice || !result.visiblePrice) {
    const legacy = evidence.match(/schema=([\d.,]+),\s*visible=([\d.,]+)/i);
    if (legacy) {
      result.schemaPrice = legacy[1];
      result.visiblePrice = legacy[2];
    }
  }

  return result;
}

export function issueProductListLabel(ruleId: string): string {
  switch (ruleId) {
    case 'GMC-X-001': return 'Bu başlığı kullanan sayfalar';
    case 'GMC-X-004': return 'Aynı açıklamayı kullanan sayfalar';
    default: return 'Etkilenen ürünler';
  }
}

export function issueProductEntries(evidence: ParsedIssueEvidence): ProductEvidenceEntry[] {
  if (evidence.products.length) return evidence.products;
  return evidence.urls.map((url) => ({ url }));
}

/** @deprecated use parseIssueEvidence */
export const parseCrossProductEvidence = parseIssueEvidence;

export type ParsedCrossProductEvidence = ParsedIssueEvidence;
