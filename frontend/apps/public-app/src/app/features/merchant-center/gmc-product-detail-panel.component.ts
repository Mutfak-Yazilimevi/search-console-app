import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { GmcIssueListComponent } from './gmc-issue-list.component';
import {
  ProductComplianceIssueDto,
  ProductComplianceProductDetailDto,
} from './merchant-center.models';

@Component({
  selector: 'SearchConsoleApp-gmc-product-detail-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonComponent, GmcIssueListComponent],
  template: `
    @if (detail()) {
      <section class="surface product-detail">
        <h2>Ürün detayı</h2>
        <p><strong>{{ detail()!.product.title }}</strong></p>
        <a [href]="detail()!.product.pageUrl" target="_blank">{{ detail()!.product.pageUrl }}</a>
        <div class="detail-actions">
          <SearchConsoleApp-button variant="secondary" size="sm" [disabled]="rescanLoading()" (click)="rescan.emit()">
            {{ rescanLoading() ? 'Taranıyor…' : 'Sayfayı yeniden tara' }}
          </SearchConsoleApp-button>
        </div>
        <div class="issue-filters">
          <select [(ngModel)]="issueSeverityFilter" name="issueSeverity">
            <option value="all">Tüm severity</option>
            <option value="Critical">Critical</option>
            <option value="Warning">Warning</option>
            <option value="Info">Info</option>
          </select>
          <select [(ngModel)]="issueRuleFilter" name="issueRule">
            <option value="all">Tüm kurallar</option>
            @for (ruleId of availableIssueRules(); track ruleId) {
              <option [value]="ruleId">{{ ruleId }}</option>
            }
          </select>
        </div>
        <SearchConsoleApp-gmc-issue-list
          [issues]="filteredIssues()"
          [runEntityId]="runEntityId()"
          [productEntityId]="detail()!.product.entityId"
          [aiEnabled]="aiEnabled()"
          [showSource]="true" />
        @if (filteredIssues().length === 0) {
          <p class="empty-filter">Seçilen filtreye uygun sorun yok.</p>
        }
      </section>
    }
  `,
  styles: [`
    .surface { padding: 1rem; margin-bottom: 1rem; border-radius: 8px; background: #fff; border: 1px solid #e2e8f0; }
    .detail-actions { margin: 0.75rem 0; }
    .issue-filters { display: flex; gap: 0.5rem; flex-wrap: wrap; margin: 0.75rem 0; }
    .issue-filters select { padding: 0.35rem 0.5rem; font-size: 0.85rem; }
    .empty-filter { color: #64748b; font-size: 0.85rem; }
  `],
})
export class GmcProductDetailPanelComponent {
  detail = input<ProductComplianceProductDetailDto | null>(null);
  runEntityId = input.required<string>();
  aiEnabled = input(false);
  rescanLoading = input(false);

  rescan = output<void>();

  issueSeverityFilter = 'all';
  issueRuleFilter = 'all';

  availableIssueRules = computed(() => {
    const issues = this.detail()?.issues ?? [];
    return [...new Set(issues.map((i) => i.ruleId))].sort();
  });

  filteredIssues = computed((): ProductComplianceIssueDto[] => {
    let issues = this.detail()?.issues ?? [];
    if (this.issueSeverityFilter !== 'all') {
      issues = issues.filter((i) => i.severity === this.issueSeverityFilter);
    }
    if (this.issueRuleFilter !== 'all') {
      issues = issues.filter((i) => i.ruleId === this.issueRuleFilter);
    }
    return issues;
  });

  resetFilters(): void {
    this.issueSeverityFilter = 'all';
    this.issueRuleFilter = 'all';
  }
}
