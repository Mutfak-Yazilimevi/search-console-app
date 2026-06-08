import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { formatCtr } from './gmc-labels';
import { GmcRunSummaryDto } from './merchant-center.models';

@Component({
  selector: 'SearchConsoleApp-gmc-feed-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="surface gmc-feed-summary">
      <h2>Google Merchant Center feed özeti</h2>
      @for (agg of summary().aggregateStatuses; track agg.reportingContext) {
        <div class="agg-row">
          <strong>{{ agg.reportingContext || 'Genel' }}</strong>
          Onaylı: {{ agg.approvedCount }} · Bekleyen: {{ agg.pendingCount }} · Red: {{ agg.disapprovedCount }}
        </div>
      }
      @if (summary().accountIssues.length) {
        <h3>Hesap sorunları</h3>
        <ul>
          @for (issue of summary().accountIssues; track issue.title) {
            <li><strong>{{ issue.severity }}</strong> — {{ issue.title }}</li>
          }
        </ul>
      }
      @if (summary().topPerformance?.length) {
        <h3>Ürün performansı (son 30 gün)</h3>
        <table class="perf-table">
          <thead>
            <tr>
              <th>Ürün</th>
              <th>Tıklama</th>
              <th>Gösterim</th>
              <th>CTR</th>
            </tr>
          </thead>
          <tbody>
            @for (row of summary().topPerformance!; track row.offerId) {
              <tr>
                <td>{{ row.title || row.offerId }}</td>
                <td>{{ row.clicks }}</td>
                <td>{{ row.impressions }}</td>
                <td>{{ formatCtr(row.clickThroughRate) }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>
  `,
  styles: [`
    .surface { padding: 1rem; margin-bottom: 1rem; border-radius: 8px; background: #fff; border: 1px solid #e2e8f0; }
    .gmc-feed-summary { font-size: 0.9rem; }
    .perf-table { width: 100%; border-collapse: collapse; margin-top: 0.5rem; font-size: 0.85rem; }
    .perf-table th, .perf-table td { border: 1px solid #e2e8f0; padding: 0.35rem 0.5rem; text-align: left; }
    .perf-table th { background: #f8fafc; }
    .gmc-feed-summary .agg-row { margin: 0.35rem 0; }
    .gmc-feed-summary h3 { margin: 0.75rem 0 0.35rem; font-size: 0.95rem; }
  `],
})
export class GmcFeedSummaryComponent {
  summary = input.required<GmcRunSummaryDto>();
  formatCtr = formatCtr;
}
