import { parseH1MultipleEvidence } from './audit-h1-evidence';
import { parseImgAltMissingEvidence } from './audit-img-alt-evidence';
import { parseIssueDetailEvidence } from './audit-issue-detail-evidence';

export type StructuredEvidenceKind = 'h1-multiple' | 'img-alt-missing' | 'issue-detail' | 'plain';

export function detectStructuredEvidenceKind(
  evidence: string | null | undefined,
): StructuredEvidenceKind {
  if (!evidence?.trim()) return 'plain';
  if (parseH1MultipleEvidence(evidence)) return 'h1-multiple';
  if (parseImgAltMissingEvidence(evidence)) return 'img-alt-missing';
  if (parseIssueDetailEvidence(evidence)) return 'issue-detail';
  return 'plain';
}

export function hasStructuredEvidenceTable(ruleId: string, evidence: string | null | undefined): boolean {
  const kind = detectStructuredEvidenceKind(evidence);
  return kind === 'h1-multiple' || kind === 'img-alt-missing' || kind === 'issue-detail';
}
