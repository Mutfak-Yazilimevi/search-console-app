import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { parseH1MultipleEvidence } from './audit-h1-evidence';

@Component({
  selector: 'SearchConsoleApp-issue-h1-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (parsed(); as data) {
      <div class="h1-evidence">
        @if (data.pageTitle) {
          <p class="h1-meta"><strong>Sayfa title:</strong> {{ data.pageTitle }}</p>
        }
        <p class="h1-meta"><strong>{{ data.count }} H1</strong> bulundu — önerilen yapı:</p>
        <table class="h1-table">
          <thead>
            <tr>
              <th>#</th>
              <th>Mevcut H1 metni</th>
            <th>Önerilen etiket</th>
              <th>Açıklama</th>
            </tr>
          </thead>
          <tbody>
            @for (row of data.headings; track row.order) {
              <tr [class.h1-primary]="row.keepAs === 'h1'">
                <td>{{ row.order }}</td>
                <td class="h1-text">{{ row.text }}</td>
                <td>
                  <span class="h1-tag" [class]="'tag-' + row.keepAs">{{ row.keepAs.toUpperCase() }}</span>
                </td>
                <td class="h1-reason">{{ row.reason }}</td>
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
    .h1-evidence { margin-top: 0.5rem; }
    .h1-meta { font-size: 0.8rem; color: #555; margin: 0 0 0.5rem; }
    .h1-table {
      width: 100%; border-collapse: collapse; font-size: 0.8rem;
      background: #fff; border: 1px solid #e8ecf0; border-radius: 6px; overflow: hidden;
    }
    .h1-table th, .h1-table td {
      padding: 0.45rem 0.55rem; border-bottom: 1px solid #f0f0f0; text-align: left; vertical-align: top;
    }
    .h1-table th { background: #f8fafc; font-weight: 600; color: #444; }
    .h1-table tr:last-child td { border-bottom: none; }
    .h1-primary { background: #f0fdf4; }
    .h1-text { max-width: 220px; word-break: break-word; font-weight: 500; }
    .h1-reason { color: #64748b; font-size: 0.75rem; }
    .h1-tag {
      display: inline-block; padding: 0.1rem 0.4rem; border-radius: 4px;
      font-size: 0.7rem; font-weight: 700; font-family: ui-monospace, monospace;
    }
    .tag-h1 { background: #dcfce7; color: #166534; }
    .tag-h2 { background: #dbeafe; color: #1e40af; }
    .tag-h3 { background: #f3e8ff; color: #6b21a8; }
    .issue-evidence {
      font-size: 0.8rem; font-family: monospace; background: #f8f8f8;
      padding: 0.3rem 0.5rem; border-radius: 4px;
    }
  `],
})
export class IssueH1TableComponent {
  evidence = input<string | null | undefined>(null);

  parsed = computed(() => parseH1MultipleEvidence(this.evidence()));
}
