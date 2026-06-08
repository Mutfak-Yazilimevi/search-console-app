import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { HintIconComponent } from '@SearchConsoleApp/shared/ui';
import { GMC_METRIC_HINTS } from './gmc-metric-hints';
import { ProductComplianceRunDto } from './merchant-center.models';

@Component({
  selector: 'SearchConsoleApp-gmc-compliance-stats',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [HintIconComponent],
  template: `
    <section class="surface stats">
      <div class="stat">
        <span class="stat-val">{{ run().complianceScore ?? '—' }}%</span>
        <span class="stat-label">
          Uyumluluk
          <SearchConsoleApp-hint [text]="hints.complianceScore" />
        </span>
      </div>
      <div class="stat">
        <span class="stat-val">{{ run().compliantCount }}</span>
        <span class="stat-label">
          Uyumlu
          <SearchConsoleApp-hint [text]="hints.compliantCount" />
        </span>
      </div>
      <div class="stat">
        <span class="stat-val">{{ run().partialCount }}</span>
        <span class="stat-label">
          Kısmen
          <SearchConsoleApp-hint [text]="hints.partialCount" />
        </span>
      </div>
      <div class="stat">
        <span class="stat-val">{{ run().nonCompliantCount }}</span>
        <span class="stat-label">
          Uyumsuz
          <SearchConsoleApp-hint [text]="hints.nonCompliantCount" />
        </span>
      </div>
      <div class="stat">
        <span class="stat-val">{{ run().siteReadinessScore ?? '—' }}%</span>
        <span class="stat-label">
          Site hazırlık
          <SearchConsoleApp-hint [text]="hints.siteReadiness" />
        </span>
      </div>
    </section>
  `,
  styles: [`
    .surface { padding: 1rem; margin-bottom: 1rem; border-radius: 8px; background: #fff; border: 1px solid #e2e8f0; }
    .stats { display: flex; flex-wrap: wrap; gap: 1rem; }
    .stat { text-align: center; min-width: 80px; }
    .stat-val { display: block; font-size: 1.5rem; font-weight: 700; }
    .stat-label {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex-wrap: wrap;
      gap: 0.1rem;
      font-size: 0.75rem;
      color: #64748b;
    }
  `],
})
export class GmcComplianceStatsComponent {
  run = input.required<ProductComplianceRunDto>();
  protected readonly hints = GMC_METRIC_HINTS;
}
