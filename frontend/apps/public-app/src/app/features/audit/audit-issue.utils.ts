import type { AuditIssueDto } from './audit.models';
import { categoryLabel, severityLabel } from './audit-labels';

export type IssueGroupMode = 'message' | 'category' | 'page' | 'severity';

export const SEVERITY_ORDER: Record<string, number> = {
  Critical: 0,
  Warning: 1,
  Info: 2,
};

export interface IssueGroup {
  key: string;
  label: string;
  issues: AuditIssueDto[];
  topSeverity: string;
  ruleId: string;
  category: string;
  fixHint: string | null;
  docUrl: string | null;
  criticalCount: number;
  warningCount: number;
  infoCount: number;
}

export function sortIssuesBySeverity(issues: AuditIssueDto[]): AuditIssueDto[] {
  return [...issues].sort(
    (a, b) => (SEVERITY_ORDER[a.severity] ?? 9) - (SEVERITY_ORDER[b.severity] ?? 9),
  );
}

export function countBySeverity(issues: AuditIssueDto[]): {
  critical: number;
  warning: number;
  info: number;
} {
  return {
    critical: issues.filter((i) => i.severity === 'Critical').length,
    warning: issues.filter((i) => i.severity === 'Warning').length,
    info: issues.filter((i) => i.severity === 'Info').length,
  };
}

export function computeSeoScoreBreakdown(issues: AuditIssueDto[]): {
  score: number;
  distinctCritical: number;
  distinctWarning: number;
  distinctInfo: number;
  penalty: number;
} {
  if (issues.length === 0) {
    return { score: 100, distinctCritical: 0, distinctWarning: 0, distinctInfo: 0, penalty: 0 };
  }

  const byRule = new Map<string, AuditIssueDto>();
  for (const issue of issues) {
    const existing = byRule.get(issue.ruleId);
    if (!existing || (SEVERITY_ORDER[issue.severity] ?? 9) < (SEVERITY_ORDER[existing.severity] ?? 9)) {
      byRule.set(issue.ruleId, issue);
    }
  }

  const distinct = [...byRule.values()];
  const distinctCritical = distinct.filter((i) => i.severity === 'Critical').length;
  const distinctWarning = distinct.filter((i) => i.severity === 'Warning').length;
  const distinctInfo = distinct.filter((i) => i.severity === 'Info').length;
  const penalty = distinctCritical * 15 + distinctWarning * 6 + Math.min(distinctInfo, 20) * 2;

  return {
    score: Math.max(0, 100 - penalty),
    distinctCritical,
    distinctWarning,
    distinctInfo,
    penalty,
  };
}

export function groupIssues(
  issues: AuditIssueDto[],
  mode: IssueGroupMode,
): IssueGroup[] {
  const buckets = new Map<string, AuditIssueDto[]>();

  for (const issue of issues) {
    const key =
      mode === 'message'
        ? issue.message
        : mode === 'category'
          ? issue.category
          : mode === 'page'
            ? (issue.pageUrl?.trim() || '__site__')
            : issue.severity;
    const list = buckets.get(key);
    if (list) list.push(issue);
    else buckets.set(key, [issue]);
  }

  const groups = [...buckets.entries()].map(([key, groupIssues]) => {
    const sorted = [...groupIssues].sort(
      (a, b) =>
        (SEVERITY_ORDER[a.severity] ?? 9) - (SEVERITY_ORDER[b.severity] ?? 9) ||
        (a.pageUrl ?? '').localeCompare(b.pageUrl ?? '', 'tr'),
    );
    const top = sorted[0];
    const counts = countBySeverity(sorted);
    return {
      key,
      label: groupLabel(mode, key),
      issues: sorted,
      topSeverity: top?.severity ?? 'Info',
      ruleId: top?.ruleId ?? '',
      category: top?.category ?? '',
      fixHint: sorted.find((i) => i.fixHint)?.fixHint ?? null,
      docUrl: top?.docUrl ?? null,
      criticalCount: counts.critical,
      warningCount: counts.warning,
      infoCount: counts.info,
    };
  });

  return groups.sort((a, b) => compareIssueGroups(mode, a, b));
}

export function filterIssuesBySeverity(
  issues: AuditIssueDto[],
  severity: string,
): AuditIssueDto[] {
  if (!severity) return issues;
  return issues.filter((i) => i.severity === severity);
}

/** SCHED-002 diff kayıtlarından yeni kritik kural kimliklerini çıkarır. */
export function extractNewCriticalRuleIds(issues: AuditIssueDto[]): Set<string> {
  const ids = new Set<string>();
  for (const issue of issues) {
    if (issue.ruleId !== 'SCHED-002') continue;
    const match = issue.message.match(/:\s*([A-Za-z0-9_-]+)\.?$/);
    if (match) ids.add(match[1]);
  }
  return ids;
}

/** Kritik sorunları kural/mesaja göre gruplar (rapor paneli için). */
export function groupCriticalIssues(issues: AuditIssueDto[]): IssueGroup[] {
  return groupIssues(
    issues.filter((i) => i.severity === 'Critical'),
    'message',
  );
}

export function uniqueRuleCount(issues: AuditIssueDto[]): number {
  return new Set(issues.map((i) => i.ruleId)).size;
}

export function groupLabel(mode: IssueGroupMode, key: string): string {
  if (mode === 'message') return key;
  if (mode === 'category') return categoryLabel(key);
  if (mode === 'severity') return severityLabel(key);
  if (key === '__site__') return 'Site geneli';
  return key;
}

export function compareIssueGroups(
  mode: IssueGroupMode,
  a: IssueGroup,
  b: IssueGroup,
): number {
  if (mode === 'message' || mode === 'severity') {
    const sevKey = mode === 'severity' ? a.key : a.topSeverity;
    const sevKeyB = mode === 'severity' ? b.key : b.topSeverity;
    const sev = (SEVERITY_ORDER[sevKey] ?? 9) - (SEVERITY_ORDER[sevKeyB] ?? 9);
    if (sev !== 0) return sev;
    return b.issues.length - a.issues.length || a.label.localeCompare(b.label, 'tr');
  }
  if (mode === 'page') {
    if (a.key === '__site__') return -1;
    if (b.key === '__site__') return 1;
    const byCritical = b.criticalCount - a.criticalCount;
    if (byCritical !== 0) return byCritical;
    return b.issues.length - a.issues.length || a.label.localeCompare(b.label, 'tr');
  }
  return a.label.localeCompare(b.label, 'tr');
}
