export interface H1HeadingRecommendation {
  order: number;
  text: string;
  keepAs: 'h1' | 'h2' | 'h3';
  reason: string;
}

export interface H1MultipleEvidence {
  type: 'h1-multiple';
  count: number;
  pageTitle?: string | null;
  headings: H1HeadingRecommendation[];
}

export function parseH1MultipleEvidence(evidence: string | null | undefined): H1MultipleEvidence | null {
  if (!evidence?.trim()) return null;
  try {
    const parsed = JSON.parse(evidence) as H1MultipleEvidence;
    if (parsed?.type === 'h1-multiple' && Array.isArray(parsed.headings)) return parsed;
  } catch {
    // legacy plain text e.g. "14 H1 tags"
  }
  return null;
}

export function h1TagLabel(tag: 'h1' | 'h2' | 'h3'): string {
  return tag.toUpperCase();
}
