export interface EvidenceItem {
  label: string;
  value: string;
  detail?: string;
  href?: string;
}

export interface IssueDetailEvidence {
  type: "issue-detail";
  headline: string;
  count?: number;
  truncated?: boolean;
  items: EvidenceItem[];
}

export const EVIDENCE_LIST_LIMIT = 30;
export const EVIDENCE_JSON_MAX = 1800;

export function buildIssueDetail(
  headline: string,
  items: EvidenceItem[],
  opts?: { count?: number; truncated?: boolean },
): string {
  let limited = items;
  let truncated = opts?.truncated ?? false;
  let json = JSON.stringify({
    type: "issue-detail",
    headline,
    count: opts?.count ?? items.length,
    truncated,
    items: limited,
  } satisfies IssueDetailEvidence);

  while (json.length > EVIDENCE_JSON_MAX && limited.length > 1) {
    limited = limited.slice(0, Math.max(1, Math.floor(limited.length * 0.7)));
    truncated = true;
    json = JSON.stringify({
      type: "issue-detail",
      headline,
      count: opts?.count ?? items.length,
      truncated,
      items: limited,
    } satisfies IssueDetailEvidence);
  }

  return json;
}

export function pageElementEvidence(
  headline: string,
  location: string,
  found: string,
  action: string,
): string {
  return buildIssueDetail(headline, [
    { label: "Konum", value: location },
    { label: "Bulunan", value: found },
    { label: "Ne yapmalı", value: action },
  ]);
}

export function listEvidence(
  headline: string,
  items: EvidenceItem[],
  totalCount?: number,
): string {
  const count = totalCount ?? items.length;
  return buildIssueDetail(headline, items, {
    count,
    truncated: count > items.length,
  });
}

export function truncateText(text: string, max = 120): string {
  const t = text.replace(/\s+/g, " ").trim();
  return t.length <= max ? t : `${t.slice(0, max - 1)}…`;
}
