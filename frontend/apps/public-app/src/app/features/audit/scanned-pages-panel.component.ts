import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import type { ScannedPageDto, AuditIssueDto } from './audit.models';

export interface PageRow extends ScannedPageDto {
  issueCount: number;
  criticalCount: number;
}

@Component({
  selector: 'SearchConsoleApp-scanned-pages-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (rows().length) {
      <details class="pages-panel" open>
        <summary>Taranan sayfalar ({{ rows().length }})</summary>
        <table class="pages-table">
          <thead>
            <tr>
              <th>URL</th>
              <th>HTTP</th>
              <th>Title</th>
              <th title="Ana sayfadan link sıçraması; sitemap sayfalarında URL yol derinliği">Derinlik</th>
              <th>Sorun</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.entityId) {
              <tr [class.has-critical]="row.criticalCount > 0">
                <td class="url"><a [href]="row.url" target="_blank" rel="noopener">{{ shortUrl(row.url) }}</a></td>
                <td [class.bad]="row.statusCode && row.statusCode >= 400">{{ row.statusCode ?? '—' }}</td>
                <td class="title">{{ row.title || '—' }}</td>
                <td>{{ row.crawlDepth }}</td>
                <td>
                  @if (row.issueCount) {
                    <span class="count" [class.critical]="row.criticalCount">{{ row.issueCount }}</span>
                  } @else {
                    —
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </details>
    }
  `,
  styles: [`
    .pages-panel { margin: 1rem 0; }
    .pages-panel summary { cursor: pointer; font-weight: 600; font-size: 0.9rem; margin-bottom: 0.5rem; }
    .pages-table {
      width: 100%; border-collapse: collapse; font-size: 0.78rem;
      background: #fff; border: 1px solid #e8ecf0; border-radius: 6px; overflow: hidden;
    }
    .pages-table th, .pages-table td {
      padding: 0.4rem 0.5rem; border-bottom: 1px solid #f0f0f0; text-align: left; vertical-align: top;
    }
    .pages-table th { background: #f8fafc; }
    .url a { color: #2563eb; text-decoration: none; word-break: break-all; }
    .url a:hover { text-decoration: underline; }
    .title { max-width: 200px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .bad { color: #dc2626; font-weight: 600; }
    .count { background: #fef3c7; padding: 0.1rem 0.35rem; border-radius: 4px; font-weight: 600; }
    .count.critical { background: #fee2e2; color: #991b1b; }
    tr.has-critical { background: #fef2f2; }
  `],
})
export class ScannedPagesPanelComponent {
  pages = input<ScannedPageDto[]>([]);
  issues = input<AuditIssueDto[]>([]);

  rows = computed((): PageRow[] => {
    const issues = this.issues();
    return this.pages().map((p) => {
      const pageIssues = issues.filter((i) => i.pageUrl === p.url || normalize(i.pageUrl) === normalize(p.url));
      return {
        ...p,
        issueCount: pageIssues.length,
        criticalCount: pageIssues.filter((i) => i.severity === 'Critical').length,
      };
    });
  });

  shortUrl(url: string): string {
    try {
      const u = new URL(url);
      const path = u.pathname + u.search;
      return path.length > 50 ? path.slice(0, 47) + '…' : path || u.hostname;
    } catch {
      return url.length > 50 ? url.slice(0, 47) + '…' : url;
    }
  }
}

function normalize(url: string): string {
  return url.replace(/\/$/, '') || url;
}
