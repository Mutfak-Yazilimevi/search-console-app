import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { formatComplianceDelta } from './gmc-labels';
import { GmcComplianceStatsComponent } from './gmc-compliance-stats.component';
import { GmcFeedSummaryComponent } from './gmc-feed-summary.component';
import { ProductComplianceRunDto } from './merchant-center.models';

@Component({
  selector: 'SearchConsoleApp-gmc-run-overview',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonComponent, GmcComplianceStatsComponent, GmcFeedSummaryComponent],
  template: `
    @if (gmcWarningMessage()) {
      <div class="banner banner-bad">{{ gmcWarningMessage() }}</div>
    }
    @if (run().status === 'Failed' && run().errorMessage) {
      <div class="banner banner-bad">{{ run().errorMessage }}</div>
    }
    @if (run().status === 'Cancelled') {
      <div class="banner banner-info">Analiz iptal edildi.</div>
    }
    @if (run().analysisMode === 'GmcConnected' && run().gmcSummary) {
      <SearchConsoleApp-gmc-feed-summary [summary]="run().gmcSummary!" />
    }
    @if (run().analysisMode === 'SiteOnly') {
      <div class="banner banner-info">
        Tahmini uyumluluk — Google feed kararını yansıtmaz. GMC bağlayarak gerçek feed durumunu görün.
      </div>
    }

    <SearchConsoleApp-gmc-compliance-stats [run]="run()" />

    @if (isRunning()) {
      <div class="progress-row">
        <p class="progress">{{ run().progressMessage ?? 'İşleniyor…' }}</p>
        @if (run().status !== 'Completed' || run().progressPhase !== 'rescanning') {
          <SearchConsoleApp-button variant="secondary" size="sm" (click)="cancel.emit()">İptal</SearchConsoleApp-button>
        }
      </div>
    }

    @if (run().comparison) {
      <div class="banner" [class]="comparisonBannerClass()">
        Önceki analize göre uyumluluk:
        {{ run().comparison!.previousComplianceScore ?? '—' }}% →
        {{ run().complianceScore ?? '—' }}%
        ({{ formatComplianceDelta(run().comparison!.complianceScoreDelta) }})
        @if (run().comparison!.newCriticalRuleIds.length) {
          · Yeni kritik: {{ run().comparison!.newCriticalRuleIds.join(', ') }}
        }
        @if (run().comparison!.resolvedCriticalRuleIds.length) {
          · Giderilen: {{ run().comparison!.resolvedCriticalRuleIds.join(', ') }}
        }
      </div>
    }
  `,
  styles: [`
    .banner { padding: 0.75rem 1rem; border-radius: 8px; margin-bottom: 1rem; }
    .banner-info { background: #eff6ff; border: 1px solid #bfdbfe; color: #1e40af; }
    .banner-good { background: #ecfdf5; border: 1px solid #a7f3d0; color: #065f46; }
    .banner-bad { background: #fef2f2; border: 1px solid #fecaca; color: #991b1b; }
    .progress { padding: 0 1rem; color: #64748b; margin: 0; }
    .progress-row { display: flex; align-items: center; gap: 1rem; padding: 0 1rem 1rem; flex-wrap: wrap; }
  `],
})
export class GmcRunOverviewComponent {
  run = input.required<ProductComplianceRunDto>();
  isRunning = input(false);
  gmcWarningMessage = input<string | null>(null);
  comparisonBannerClass = input('banner-info');

  cancel = output<void>();

  formatComplianceDelta = formatComplianceDelta;
}
