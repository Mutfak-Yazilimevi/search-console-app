import { ProductComplianceDetailDto } from './merchant-center.models';

export function buildComplianceChecklistMarkdown(detail: ProductComplianceDetailDto): string {
  const lines: string[] = [
    `# GMC Uyumluluk Checklist`,
    ``,
    `Site: ${detail.run.inputUrl}`,
    `Tarih: ${detail.run.completedAt ?? detail.run.createdAt}`,
    `Uyumluluk: ${detail.run.complianceScore ?? '—'}% · Site hazırlık: ${detail.run.siteReadinessScore ?? '—'}%`,
    ``,
    `## Öncelikli aksiyonlar`,
  ];

  if ((detail.run.priorityActions ?? []).length === 0) {
    lines.push(`- (yok)`);
  } else {
    for (const p of detail.run.priorityActions) {
      lines.push(`- [ ] **${p.affectedCount} ürün** — ${p.message}`);
      lines.push(`  - ${p.fixHint}`);
    }
  }

  appendIssues(lines, 'Site gereksinimleri', detail.siteIssues);
  appendIssues(lines, 'Çapraz ürün sorunları', detail.crossProductIssues ?? []);
  appendIssues(lines, 'Feed eşleştirme', detail.feedIssues ?? []);

  return lines.join('\n');
}

function appendIssues(
  lines: string[],
  title: string,
  issues: ProductComplianceDetailDto['siteIssues'],
): void {
  if (!issues.length) return;
  lines.push(``, `## ${title}`);
  for (const issue of issues) {
    lines.push(`- [ ] **${issue.ruleId}** (${issue.severity}) — ${issue.message}`);
    lines.push(`  - ${issue.fixHint}`);
  }
}

export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}
