import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { AuthService } from '@SearchConsoleApp/shared/core';
import { IntegrationItemDto } from '../audit/audit-api.service';
import { IntegrationStatusPanelComponent } from '../audit/integration-status-panel.component';
import { OAuthSetupGuideComponent } from '../auth/oauth-setup-guide.component';
import { OAuthSetupGuide } from '../auth/oauth-setup.models';
import {
  MerchantCenterAccountDto,
  ProductComplianceRunSummaryDto,
} from './merchant-center.models';

@Component({
  selector: 'SearchConsoleApp-merchant-center-hero',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    ButtonComponent,
    IntegrationStatusPanelComponent,
    OAuthSetupGuideComponent,
  ],
  template: `
    <section class="gmc-hero surface">
      <nav class="gmc-nav">
        <a routerLink="/">← SEO Denetimi</a>
      </nav>
      <h1>Merchant Center Ürün Uyumluluğu</h1>
      <p class="subtitle">Ürünlerinizin Google Merchant Center kurallarına uygunluğunu analiz edin.</p>

      @if (integrationItems().length) {
        <SearchConsoleApp-integration-status-panel
          title="GMC entegrasyonları"
          [editable]="true"
          [collapsible]="true"
          [defaultOpen]="true"
          editHint="Toggle ile entegrasyonu açıp kapatabilir, Düzenle ile API anahtarı girebilirsiniz. Merchant Center OAuth için Client ID/secret kaydedin; ardından hesabınıza bağlanın."
          [items]="integrationItems()"
          (changed)="integrationsChanged.emit()" />
      }

      <form class="gmc-form" (ngSubmit)="start.emit()">
        <input
          type="url"
          [ngModel]="url()"
          (ngModelChange)="urlChange.emit($event)"
          name="url"
          placeholder="https://magaza-ornegi.com"
          required />
        @if (gmcConnected()) {
          <select
            [ngModel]="selectedAccountId()"
            (ngModelChange)="selectedAccountIdChange.emit($event)"
            name="account">
            <option value="">GMC hesabı (opsiyonel)</option>
            @for (a of gmcAccounts(); track a.accountId) {
              <option [value]="a.accountId">{{ a.name }} ({{ a.accountId }})</option>
            }
          </select>
        }
        <SearchConsoleApp-button variant="primary" type="submit" [disabled]="loading()">
          {{ loading() ? 'Başlatılıyor…' : 'Analiz Et' }}
        </SearchConsoleApp-button>
      </form>

      @if (!gmcConnected() && auth.isAuthenticated()) {
        <SearchConsoleApp-button variant="secondary" (click)="connectGmc.emit()">
          Merchant Center'a Bağlan
        </SearchConsoleApp-button>
      }
      @if (gmcConnected()) {
        <p class="gmc-connected">
          GMC bağlı
          <button type="button" class="link-btn" (click)="disconnectGmc.emit()">Bağlantıyı kes</button>
        </p>
      }
      @if (error()) {
        <p class="error">{{ error() }}</p>
      }
      @if (oauthGuide()) {
        <SearchConsoleApp-oauth-setup-guide [guide]="oauthGuide()!" />
      }

      @if (recentRuns().length) {
        <div class="recent-runs">
          <span class="recent-label">{{ auth.isAuthenticated() ? 'Son analizleriniz:' : 'Son analizler:' }}</span>
          @for (r of recentRuns(); track r.entityId) {
            <button type="button" class="recent-chip" (click)="openRun.emit(r.entityId)">
              {{ r.inputUrl }} · {{ r.complianceScore ?? '—' }}%
            </button>
          }
        </div>
      }
    </section>
  `,
  styles: [`
    .gmc-hero { padding: 1.5rem; margin-bottom: 1rem; }
    .gmc-nav a { font-size: 0.85rem; color: #4285f4; }
    .subtitle { color: #64748b; margin-bottom: 1rem; }
    .gmc-form { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-bottom: 0.75rem; }
    .gmc-form input, .gmc-form select { flex: 1; min-width: 200px; padding: 0.5rem; }
    .gmc-connected { font-size: 0.85rem; color: #166534; margin: 0.5rem 0 0; }
    .recent-runs { display: flex; flex-wrap: wrap; gap: 0.35rem; align-items: center; margin-top: 0.75rem; }
    .recent-label { font-size: 0.8rem; color: #64748b; margin-right: 0.25rem; }
    .recent-chip { font-size: 0.75rem; padding: 0.25rem 0.5rem; border-radius: 6px; border: 1px solid #e2e8f0; background: #fff; cursor: pointer; color: #334155; }
    .recent-chip:hover { border-color: #4285f4; color: #1d4ed8; }
    .error { color: #c0392b; }
    .link-btn { background: none; border: none; color: #4285f4; cursor: pointer; text-decoration: underline; }
  `],
})
export class MerchantCenterHeroComponent {
  auth = inject(AuthService);

  integrationItems = input<IntegrationItemDto[]>([]);
  url = input('');
  selectedAccountId = input('');
  gmcConnected = input(false);
  gmcAccounts = input<MerchantCenterAccountDto[]>([]);
  loading = input(false);
  error = input<string | null>(null);
  oauthGuide = input<OAuthSetupGuide | null>(null);
  recentRuns = input<ProductComplianceRunSummaryDto[]>([]);

  urlChange = output<string>();
  selectedAccountIdChange = output<string>();
  start = output<void>();
  connectGmc = output<void>();
  disconnectGmc = output<void>();
  openRun = output<string>();
  integrationsChanged = output<void>();
}
