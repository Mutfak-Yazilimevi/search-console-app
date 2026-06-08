import { ChangeDetectionStrategy, Component, input, output, viewChild } from '@angular/core';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { GmcAiSummaryComponent } from './gmc-ai-summary.component';
import { GmcIssueListComponent } from './gmc-issue-list.component';
import { GmcProductDetailPanelComponent } from './gmc-product-detail-panel.component';
import { GmcProductTableComponent } from './gmc-product-table.component';
import {
  GmcBulkAiItemDto,
  ProductComplianceDetailDto,
  ProductComplianceItemDto,
  ProductComplianceProductDetailDto,
} from './merchant-center.models';

@Component({
  selector: 'SearchConsoleApp-gmc-run-completed',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ButtonComponent,
    GmcAiSummaryComponent,
    GmcIssueListComponent,
    GmcProductTableComponent,
    GmcProductDetailPanelComponent,
  ],
  template: `
    <div class="export-bar">
      <SearchConsoleApp-button variant="secondary" size="sm" (click)="exportJson.emit()">JSON indir</SearchConsoleApp-button>
      <SearchConsoleApp-button variant="secondary" size="sm" (click)="exportChecklist.emit()">Checklist indir</SearchConsoleApp-button>
      <SearchConsoleApp-button variant="secondary" size="sm" (click)="exportHtml.emit()">HTML / PDF rapor</SearchConsoleApp-button>
      <SearchConsoleApp-button variant="secondary" size="sm" [disabled]="bulkAiLoading() || !aiEnabled()" (click)="bulkAi.emit()">
        {{ bulkAiLoading() ? 'AI…' : 'Top 5 başlık (AI)' }}
      </SearchConsoleApp-button>
    </div>
    @if (bulkAiResults().length) {
      <section class="surface bulk-ai">
        <h2>Toplu AI başlık önerileri</h2>
        @for (item of bulkAiResults(); track item.productEntityId) {
          <div class="bulk-ai-item">
            <strong>{{ item.title || item.pageUrl }}</strong>
            @if (item.error) {
              <p class="error">{{ item.error }}</p>
            } @else if (item.result) {
              <pre>{{ item.result.content }}</pre>
            }
          </div>
        }
      </section>
    }

    <section class="surface">
      <h2>Öncelikli aksiyonlar</h2>
      @if ((detail().run.priorityActions ?? []).length === 0) {
        <p>Kritik toplu sorun bulunamadı.</p>
      } @else {
        <ul class="priority-list">
          @for (p of detail().run.priorityActions; track p.ruleId) {
            <li>
              <strong>{{ p.affectedCount }} ürün</strong> — {{ p.message }}
              <div class="fix-hint">{{ p.fixHint }}</div>
            </li>
          }
        </ul>
      }
      <SearchConsoleApp-gmc-ai-summary [runEntityId]="detail().run.entityId" [enabled]="aiEnabled()" />
    </section>

    @if ((detail().feedIssues ?? []).length) {
      <section class="surface">
        <h2>Feed eşleştirme</h2>
        <SearchConsoleApp-gmc-issue-list
          [issues]="detail().feedIssues ?? []"
          [runEntityId]="detail().run.entityId"
          [aiEnabled]="aiEnabled()"
          [showAi]="false" />
      </section>
    }

    @if ((detail().crossProductIssues ?? []).length) {
      <section class="surface">
        <h2>Çapraz ürün sorunları</h2>
        <SearchConsoleApp-gmc-issue-list
          [issues]="detail().crossProductIssues ?? []"
          [runEntityId]="detail().run.entityId"
          [aiEnabled]="aiEnabled()" />
      </section>
    }

    @if ((detail().siteIssues ?? []).length) {
      <section class="surface">
        <h2>Site gereksinimleri</h2>
        <SearchConsoleApp-gmc-issue-list
          [issues]="detail().siteIssues ?? []"
          [runEntityId]="detail().run.entityId"
          [aiEnabled]="aiEnabled()" />
      </section>
    }

    <SearchConsoleApp-gmc-product-table
      [products]="products()"
      [totalProducts]="detail().run.totalProducts"
      [statusFilter]="statusFilter()"
      [hasMore]="hasMoreProducts()"
      [loadingMore]="loadingMore()"
      (statusFilterChange)="statusFilterChange.emit($event)"
      (loadMore)="loadMore.emit()"
      (selectProduct)="selectProduct.emit($event)" />

    <SearchConsoleApp-gmc-product-detail-panel
      #detailPanel
      [detail]="productDetail()"
      [runEntityId]="detail().run.entityId"
      [aiEnabled]="aiEnabled()"
      [rescanLoading]="rescanLoading()"
      (rescan)="rescanProduct.emit()" />
  `,
  styles: [`
    .surface { padding: 1rem; margin-bottom: 1rem; border-radius: 8px; background: #fff; border: 1px solid #e2e8f0; }
    .export-bar { display: flex; justify-content: flex-end; gap: 0.5rem; margin-bottom: 0.5rem; flex-wrap: wrap; }
    .bulk-ai { font-size: 0.9rem; margin-bottom: 1rem; }
    .bulk-ai-item { margin-bottom: 1rem; padding-bottom: 0.75rem; border-bottom: 1px solid #e2e8f0; }
    .bulk-ai-item pre { font-size: 0.8rem; white-space: pre-wrap; margin: 0.35rem 0 0; }
    .error { color: #c0392b; }
    .priority-list { padding-left: 1.2rem; }
    .fix-hint { font-size: 0.85rem; color: #475569; margin: 0.25rem 0; }
  `],
})
export class GmcRunCompletedComponent {
  detail = input.required<ProductComplianceDetailDto>();
  products = input.required<ProductComplianceItemDto[]>();
  productDetail = input<ProductComplianceProductDetailDto | null>(null);
  aiEnabled = input(false);
  statusFilter = input('all');
  hasMoreProducts = input(false);
  loadingMore = input(false);
  bulkAiLoading = input(false);
  bulkAiResults = input<GmcBulkAiItemDto[]>([]);
  rescanLoading = input(false);

  statusFilterChange = output<string>();
  loadMore = output<void>();
  selectProduct = output<string>();
  exportJson = output<void>();
  exportChecklist = output<void>();
  exportHtml = output<void>();
  bulkAi = output<void>();
  rescanProduct = output<void>();

  detailPanel = viewChild(GmcProductDetailPanelComponent);

  resetProductFilters(): void {
    this.detailPanel()?.resetFilters();
  }
}
