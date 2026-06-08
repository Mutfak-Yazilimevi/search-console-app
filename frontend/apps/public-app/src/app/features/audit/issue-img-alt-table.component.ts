import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { imgAltProblemLabel, parseImgAltMissingEvidence } from './audit-img-alt-evidence';

@Component({
  selector: 'SearchConsoleApp-issue-img-alt-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (parsed(); as data) {
      <div class="img-alt-evidence">
        <p class="img-alt-meta">
          <strong>{{ data.count }} görsel</strong> alt metni eksik veya boş
          @if (data.truncated) {
            <span class="img-alt-truncated">(ilk {{ data.images.length }} gösteriliyor)</span>
          }
        </p>
        <table class="img-alt-table">
          <thead>
            <tr>
              <th>#</th>
              <th>Görsel (src)</th>
              <th>Durum</th>
              <th>Öneri</th>
            </tr>
          </thead>
          <tbody>
            @for (row of data.images; track row.order) {
              <tr>
                <td>{{ row.order }}</td>
                <td class="img-src">
                  @if (isAbsoluteUrl(row.src)) {
                    <a [href]="row.src" target="_blank" rel="noopener">{{ displaySrc(row.src) }}</a>
                  } @else {
                    <code>{{ displaySrc(row.src) }}</code>
                  }
                </td>
                <td>
                  <span class="problem-badge" [class]="'problem-' + row.problem">
                    {{ problemLabel(row.problem) }}
                  </span>
                </td>
                <td class="img-suggestion">{{ row.suggestion }}</td>
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
    .img-alt-evidence { margin-top: 0.5rem; }
    .img-alt-meta { font-size: 0.8rem; color: #555; margin: 0 0 0.5rem; }
    .img-alt-truncated { color: #94a3b8; font-weight: normal; }
    .img-alt-table {
      width: 100%; border-collapse: collapse; font-size: 0.8rem;
      background: #fff; border: 1px solid #e8ecf0; border-radius: 6px; overflow: hidden;
    }
    .img-alt-table th, .img-alt-table td {
      padding: 0.45rem 0.55rem; border-bottom: 1px solid #f0f0f0; text-align: left; vertical-align: top;
    }
    .img-alt-table th { background: #f8fafc; font-weight: 600; color: #444; }
    .img-alt-table tr:last-child td { border-bottom: none; }
    .img-src { max-width: 280px; word-break: break-all; }
    .img-src code { font-size: 0.75rem; background: #f1f5f9; padding: 0.1rem 0.25rem; border-radius: 3px; }
    .img-src a { color: #2563eb; text-decoration: none; font-size: 0.75rem; }
    .img-src a:hover { text-decoration: underline; }
    .img-suggestion { color: #64748b; font-size: 0.75rem; max-width: 240px; }
    .problem-badge {
      display: inline-block; padding: 0.1rem 0.35rem; border-radius: 4px;
      font-size: 0.68rem; font-weight: 600; white-space: nowrap;
    }
    .problem-missing { background: #fef3c7; color: #92400e; }
    .problem-empty { background: #fee2e2; color: #991b1b; }
    .issue-evidence {
      font-size: 0.8rem; font-family: monospace; background: #f8f8f8;
      padding: 0.3rem 0.5rem; border-radius: 4px;
    }
  `],
})
export class IssueImgAltTableComponent {
  evidence = input<string | null | undefined>(null);

  parsed = computed(() => parseImgAltMissingEvidence(this.evidence()));

  problemLabel(problem: 'missing' | 'empty'): string {
    return imgAltProblemLabel(problem);
  }

  isAbsoluteUrl(src: string): boolean {
    return /^https?:\/\//i.test(src);
  }

  displaySrc(src: string): string {
    if (src.length <= 120) return src;
    return src.slice(0, 117) + '…';
  }
}
