export interface MissingAltImageRow {
  order: number;
  src: string;
  problem: 'missing' | 'empty';
  suggestion: string;
}

export interface ImgAltMissingEvidence {
  type: 'img-alt-missing';
  count: number;
  truncated?: boolean;
  images: MissingAltImageRow[];
}

export function parseImgAltMissingEvidence(evidence: string | null | undefined): ImgAltMissingEvidence | null {
  if (!evidence?.trim()) return null;
  try {
    const parsed = JSON.parse(evidence) as ImgAltMissingEvidence;
    if (parsed?.type === 'img-alt-missing' && Array.isArray(parsed.images)) return parsed;
  } catch {
    // legacy plain text e.g. "9 görsel"
  }
  return null;
}

export function imgAltProblemLabel(problem: 'missing' | 'empty'): string {
  return problem === 'empty' ? 'Boş alt' : 'Alt yok';
}
