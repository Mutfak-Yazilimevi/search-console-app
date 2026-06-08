export interface IssueDetailItem {
  label: string;
  value: string;
  detail?: string;
  href?: string;
}

export interface IssueDetailEvidence {
  type: 'issue-detail';
  headline: string;
  count?: number;
  truncated?: boolean;
  items: IssueDetailItem[];
}

export function parseIssueDetailEvidence(evidence: string | null | undefined): IssueDetailEvidence | null {
  if (!evidence?.trim()) return null;
  try {
    const parsed = JSON.parse(evidence) as IssueDetailEvidence;
    if (parsed?.type === 'issue-detail' && Array.isArray(parsed.items) && parsed.headline) {
      return parsed;
    }
  } catch {
    // plain text legacy evidence
  }
  return null;
}

export function isStructuredEvidence(evidence: string | null | undefined): boolean {
  if (!evidence?.trim()) return false;
  return evidence.trimStart().startsWith('{');
}
