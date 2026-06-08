import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { parseIssueDetailEvidence } from './audit-issue-detail-evidence';

@Component({
  selector: 'SearchConsoleApp-issue-detail-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (parsed(); as data) {
      <div class="detail-evidence">
        <p class="detail-headline">{{ data.headline }}</p>
        @if (data.truncated && data.count) {
          <p class="detail-meta">({{ data.items.length }} / {{ data.count }} gösteriliyor)</p>
        }
        <table class="detail-table">
          <thead>
            <tr>
              <th>Alan</th>
              <th>Değer</th>
              @if (hasDetailColumn()) {
                <th>Not</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (row of data.items; track row.label + row.value) {
              <tr>
                <td class="detail-label">{{ row.label }}</td>
                <td class="detail-value">
                  @if (row.href) {
                    <a [href]="row.href" target="_blank" rel="noopener">{{ displayValue(row.value) }}</a>
                  } @else {
                    {{ displayValue(row.value) }}
                  }
                </td>
                @if (hasDetailColumn()) {
                  <td class="detail-note">{{ row.detail ?? '' }}</td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else if (evidence()) {
      <p class="issue-evidence">{{ evidence() }}</p>
    }
  `,
  styles: [`
    .detail-evidence { margin-top: 0.5rem; }
    .detail-headline { font-size: 0.82rem; font-weight: 600; color: #334155; margin: 0 0 0.35rem; }
    .detail-meta { font-size: 0.75rem; color: #94a3b8; margin: 0 0 0.4rem; }
    .detail-table {
      width: 100%; border-collapse: collapse; font-size: 0.8rem;
      background: #fff; border: 1px solid #e8ecf0; border-radius: 6px; overflow: hidden;
    }
    .detail-table th, .detail-table td {
      padding: 0.45rem 0.55rem; border-bottom: 1px solid #f0f0f0; text-align: left; vertical-align: top;
    }
    .detail-table th { background: #f8fafc; font-weight: 600; color: #444; white-space: nowrap; }
    .detail-table tr:last-child td { border-bottom: none; }
    .detail-label { color: #64748b; font-weight: 500; min-width: 72px; }
    .detail-value { word-break: break-word; max-width: 340px; }
    .detail-value a { color: #2563eb; text-decoration: none; font-size: 0.78rem; }
    .detail-value a:hover { text-decoration: underline; }
    .detail-note { color: #64748b; font-size: 0.75rem; max-width: 200px; }
    .issue-evidence {
      font-size: 0.8rem; font-family: monospace; background: #f8f8f8;
      padding: 0.3rem 0.5rem; border-radius: 4px; white-space: pre-wrap; word-break: break-word;
    }
  `],
})
export class IssueDetailTableComponent {
  evidence = input<string | null | undefined>(null);

  parsed = computed(() => parseIssueDetailEvidence(this.evidence()));

  hasDetailColumn = computed(() => (this.parsed()?.items ?? []).some((i) => !!i.detail));

  displayValue(value: string): string {
    return value.length <= 200 ? value : value.slice(0, 197) + '…';
  }
}
