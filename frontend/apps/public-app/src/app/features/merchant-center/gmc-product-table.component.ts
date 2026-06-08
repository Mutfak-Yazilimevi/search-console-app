import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonComponent, HintIconComponent } from '@SearchConsoleApp/shared/ui';
import { GMC_METRIC_HINTS } from './gmc-metric-hints';
import { ProductComplianceItemDto } from './merchant-center.models';

@Component({
  selector: 'SearchConsoleApp-gmc-product-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonComponent, HintIconComponent],
  template: `
    <section class="surface">
      <div class="table-head">
        <h2>Ürünler ({{ products().length }} / {{ totalProducts() }})</h2>
        <div class="table-filters">
          <select
            [ngModel]="statusFilter()"
            (ngModelChange)="statusFilterChange.emit($event)"
            name="filter">
            <option value="all">Tümü</option>
            <option value="NonCompliant">Yalnızca uyumsuz</option>
            <option value="Partial">Kısmen uyumlu</option>
            <option value="Compliant">Uyumlu</option>
          </select>
        </div>
      </div>
      <table>
        <thead>
          <tr>
            <th>Ürün</th>
            <th>Durum <SearchConsoleApp-hint [text]="hints.productStatus" /></th>
            <th>Skor <SearchConsoleApp-hint [text]="hints.productScore" /></th>
            <th>Sorun</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (p of products(); track p.entityId) {
            <tr>
              <td>
                <div class="title">{{ p.title || '(başlık yok)' }}</div>
                <a [href]="p.pageUrl" target="_blank" rel="noopener">{{ p.pageUrl }}</a>
                @if (p.gmcStatus) {
                  <span class="gmc-badge">GMC: {{ p.gmcStatus }}</span>
                }
              </td>
              <td><span class="status-badge">{{ p.status }}</span></td>
              <td>{{ p.complianceScore }}%</td>
              <td>{{ p.issueCount }}</td>
              <td>
                <button type="button" class="link-btn" (click)="selectProduct.emit(p.entityId)">Detay</button>
              </td>
            </tr>
          }
        </tbody>
      </table>
      @if (hasMore()) {
        <div class="load-more">
          <SearchConsoleApp-button variant="secondary" size="sm" [disabled]="loadingMore()" (click)="loadMore.emit()">
            {{ loadingMore() ? 'Yükleniyor…' : 'Daha fazla yükle' }}
          </SearchConsoleApp-button>
        </div>
      }
    </section>
  `,
  styles: [`
    .surface { padding: 1rem; margin-bottom: 1rem; border-radius: 8px; background: #fff; border: 1px solid #e2e8f0; }
    .table-head { display: flex; justify-content: space-between; align-items: center; }
    .table-filters { display: flex; gap: 0.5rem; flex-wrap: wrap; }
    .load-more { margin-top: 0.75rem; text-align: center; }
    table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    th, td { text-align: left; padding: 0.5rem; border-bottom: 1px solid #f1f5f9; vertical-align: top; }
    .title { font-weight: 600; }
    td a { font-size: 0.75rem; word-break: break-all; }
    .link-btn { background: none; border: none; color: #4285f4; cursor: pointer; text-decoration: underline; }
    .gmc-badge { display: inline-block; font-size: 0.7rem; background: #f0fdf4; padding: 0.1rem 0.35rem; border-radius: 4px; }
  `],
})
export class GmcProductTableComponent {
  products = input.required<ProductComplianceItemDto[]>();
  totalProducts = input(0);
  statusFilter = input('all');
  hasMore = input(false);
  loadingMore = input(false);

  statusFilterChange = output<string>();
  loadMore = output<void>();
  selectProduct = output<string>();

  protected readonly hints = GMC_METRIC_HINTS;
}
