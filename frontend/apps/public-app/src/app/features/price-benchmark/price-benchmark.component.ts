import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { PriceBenchmarkApiService } from './price-benchmark-api.service';
import { PriceBenchmarkPollService } from './price-benchmark-poll.service';
import { PriceBenchmarkDetailDto, PriceBenchmarkItemDto } from './price-benchmark.models';
import {
  isComparePhase,
  isDiscoverPhase,
  isPriceBenchmarkRunning,
  isProductCompared,
  marketPositionClass,
  marketPositionLabel,
  priceBenchmarkStatusLabel,
} from './price-benchmark-labels';
import {
  getMaxOffer,
  getMinOffer,
  parseShoppingOffers,
  ShoppingOfferView,
} from './price-benchmark-offers.utils';

@Component({
  selector: 'SearchConsoleApp-price-benchmark',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, DecimalPipe, RouterLink, ButtonComponent],
  template: `
    <section class="pb-hero surface">
      <nav class="pb-nav">
        <a routerLink="/">← SEO Denetimi</a>
        <span class="sep">·</span>
        <a routerLink="/merchant-center">Ürün Uyumluluğu</a>
      </nav>
      <h1>Ürün Fiyat Analizi</h1>
      <p class="subtitle">
        Önce sitedeki tüm ürünler listelenir; ardından her ürün Google Shopping'de isim bazlı aranıp
        piyasa fiyatlarıyla karşılaştırılır.
      </p>

      <form class="pb-form" (submit)="onSubmit($event)">
        <input
          type="text"
          name="url"
          [(ngModel)]="urlInput"
          placeholder="ornek.com veya https://ornek.com"
          [disabled]="loading() || isRunning()"
          required />
        <SearchConsoleApp-button variant="primary" type="submit" [disabled]="loading() || isRunning()">
          {{ loading() ? 'Başlatılıyor…' : 'Analizi Başlat' }}
        </SearchConsoleApp-button>
      </form>

      @if (error()) {
        <p class="error">{{ error() }}</p>
      }
    </section>

    @if (detail()) {
      <section class="pb-results surface">
        <header class="results-header">
          <div>
            <h2>{{ detail()!.run.normalizedUrl }}</h2>
            <span class="status" [class]="statusClass()">{{ statusLabel() }}</span>
          </div>
          <div class="results-actions">
            @if (isRunning()) {
              <SearchConsoleApp-button variant="danger" size="sm" [disabled]="stopping()" (click)="onCancel()">
                {{ stopping() ? 'Durduruluyor…' : 'Analizi Durdur' }}
              </SearchConsoleApp-button>
            }
          </div>
        </header>

        <div class="phase-steps">
          <div class="phase" [class.active]="isDiscovering()" [class.done]="isComparing() || isCompleted()">
            <span class="phase-num">1</span>
            <span class="phase-label">Ürünleri listele</span>
          </div>
          <span class="phase-arrow">→</span>
          <div class="phase" [class.active]="isComparing()" [class.done]="isCompleted()">
            <span class="phase-num">2</span>
            <span class="phase-label">Fiyat karşılaştır</span>
          </div>
        </div>

        <div class="stats">
          <div class="stat">
            <strong>{{ detail()!.run.totalProducts }}</strong>
            <span>Ürün</span>
          </div>
          @if (isComparing() || isCompleted()) {
            <div class="stat below">
              <strong>{{ belowCount() }}</strong>
              <span>Piyasanın altında</span>
            </div>
            <div class="stat average">
              <strong>{{ averageCount() }}</strong>
              <span>Ortalama</span>
            </div>
            <div class="stat above">
              <strong>{{ aboveCount() }}</strong>
              <span>Piyasanın üstünde</span>
            </div>
            <div class="stat">
              <strong>{{ comparedCount() }}</strong>
              <span>Karşılaştırıldı</span>
            </div>
          }
        </div>

        @if (hasShoppingErrors() && (isComparing() || isCompleted())) {
          <p class="warn-banner">
            Google Shopping sonuçları alınamadı. Docker ortamında Google CAPTCHA engeli yaygındır —
            <code>.env</code> dosyasına <code>SERP_API_KEY</code> ekleyip stack'i yeniden başlatın.
          </p>
        }

        @if (isRunning() || detail()!.run.progressMessage) {
          <p class="progress">{{ detail()!.run.progressMessage }}</p>
        }

        @if (allProducts().length > 0) {
          <div class="toolbar">
            @if (isComparing() || isCompleted()) {
              <label>
                Konum:
                <select [ngModel]="positionFilter()" (ngModelChange)="positionFilter.set($event)">
                  <option value="all">Tümü</option>
                  <option value="below">Piyasanın altında</option>
                  <option value="average">Ortalama</option>
                  <option value="above">Piyasanın üstünde</option>
                  <option value="unknown">Bilinmiyor</option>
                </select>
              </label>
            }
            <span class="toolbar-meta">{{ filteredProducts().length }} ürün gösteriliyor</span>
          </div>

          <div class="table-wrap">
            <table class="pb-table">
              <thead>
                <tr>
                  <th>Ürün</th>
                  <th>Sizin fiyat</th>
                  <th>En ucuz</th>
                  <th>En pahalı</th>
                  <th>Ağırlıklı ort.</th>
                  <th>Fark</th>
                  <th>Konum</th>
                </tr>
              </thead>
              <tbody>
                @for (p of filteredProducts(); track p.entityId) {
                  <tr>
                    <td class="product-cell">
                      <a [href]="p.pageUrl" target="_blank" rel="noopener" class="product-title">
                        {{ p.title || p.pageUrl }}
                      </a>
                      @if (p.shoppingError) {
                        <span class="shop-err" [title]="p.shoppingError">{{ shoppingErrorLabel(p.shoppingError) }}</span>
                      }
                    </td>
                    <td>{{ formatPrice(p.ourPrice, p.priceCurrency) }}</td>
                    <td>
                      @if (!productCompared(p) && (isComparing() || isDiscovering())) {
                        <span class="pending-cell">{{ isDiscovering() ? 'Sırada…' : 'Karşılaştırılıyor…' }}</span>
                      } @else if (minOffer(p); as offer) {
                        <div class="offer-cell">
                          @if (offer.thumbnail) {
                            <img [src]="offer.thumbnail" alt="" class="offer-thumb" loading="lazy" />
                          } @else {
                            <span class="offer-thumb placeholder" aria-hidden="true">📦</span>
                          }
                          <div class="offer-meta">
                            <span class="offer-price">{{ formatPrice(offer.price, offer.currency || p.priceCurrency) }}</span>
                            @if (offer.source && offer.link) {
                              <a [href]="offer.link" target="_blank" rel="noopener" class="offer-brand">{{ offer.source }}</a>
                            } @else if (offer.source) {
                              <span class="offer-brand-text">{{ offer.source }}</span>
                            }
                          </div>
                        </div>
                      } @else { — }
                    </td>
                    <td>
                      @if (!productCompared(p) && (isComparing() || isDiscovering())) {
                        <span class="pending-cell">{{ isDiscovering() ? 'Sırada…' : 'Karşılaştırılıyor…' }}</span>
                      } @else if (maxOffer(p); as offer) {
                        <div class="offer-cell">
                          @if (offer.thumbnail) {
                            <img [src]="offer.thumbnail" alt="" class="offer-thumb" loading="lazy" />
                          } @else {
                            <span class="offer-thumb placeholder" aria-hidden="true">📦</span>
                          }
                          <div class="offer-meta">
                            <span class="offer-price">{{ formatPrice(offer.price, offer.currency || p.priceCurrency) }}</span>
                            @if (offer.source && offer.link) {
                              <a [href]="offer.link" target="_blank" rel="noopener" class="offer-brand">{{ offer.source }}</a>
                            } @else if (offer.source) {
                              <span class="offer-brand-text">{{ offer.source }}</span>
                            }
                          </div>
                        </div>
                      } @else { — }
                    </td>
                    <td>
                      @if (!productCompared(p) && (isComparing() || isDiscovering())) {
                        <span class="pending-cell">{{ isDiscovering() ? 'Sırada…' : 'Karşılaştırılıyor…' }}</span>
                      } @else {
                        <div class="avg-cell">
                          <span>{{ formatPrice(p.weightedAvgMarketPrice, p.priceCurrency) }}</span>
                          @if (p.marketOfferCount > 0) {
                            <button type="button" class="offers-btn" (click)="openOffersModal(p)" title="Tüm teklifleri gör">
                              Teklifler
                            </button>
                          }
                        </div>
                      }
                    </td>
                    <td [class.delta-low]="(p.deltaPercent ?? 0) < -5" [class.delta-high]="(p.deltaPercent ?? 0) > 5">
                      @if (!productCompared(p) && (isComparing() || isDiscovering())) {
                        <span class="pending-cell">—</span>
                      } @else if (p.deltaPercent != null) {
                        {{ p.deltaPercent > 0 ? '+' : '' }}{{ p.deltaPercent | number:'1.1-1' }}%
                      } @else { — }
                    </td>
                    <td>
                      @if (!productCompared(p) && (isComparing() || isDiscovering())) {
                        <span class="pending-cell">{{ isDiscovering() ? 'Bekliyor' : 'Karşılaştırılıyor…' }}</span>
                      } @else {
                        <span class="pos-badge" [class]="positionClass(p.marketPosition)">
                          {{ positionLabel(p.marketPosition) }}
                        </span>
                        @if (p.marketOfferCount > 0) {
                          <span class="offer-count">{{ p.marketOfferCount }} teklif</span>
                        }
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>

          @if (hasMoreProducts()) {
            <p class="load-more">
              <SearchConsoleApp-button variant="secondary" [disabled]="loadingMore()" (click)="loadMore()">
                {{ loadingMore() ? 'Yükleniyor…' : 'Daha fazla ürün yükle' }}
              </SearchConsoleApp-button>
            </p>
          }
        } @else if (!isRunning() && detail()!.run.status === 'Completed') {
          <p class="empty">Ürün bulunamadı. Sitede Product schema veya görünür fiyat içeren sayfalar taranır.</p>
        }

        @if (detail()!.run.errorMessage) {
          <p class="error">{{ detail()!.run.errorMessage }}</p>
        }
      </section>
    }

    @if (offersModal(); as modal) {
      <div class="modal-backdrop" (click)="closeOffersModal()" role="presentation">
        <div class="modal-panel" role="dialog" aria-modal="true" [attr.aria-label]="modal.productTitle + ' teklifleri'" (click)="$event.stopPropagation()">
          <header class="modal-header">
            <h3>{{ modal.productTitle }}</h3>
            <button type="button" class="modal-close" (click)="closeOffersModal()" aria-label="Kapat">×</button>
          </header>
          <p class="modal-sub">{{ modal.offers.length }} Google Shopping teklifi</p>
          <ul class="modal-offers">
            @for (o of modal.offers; track o.position + o.link) {
              <li class="modal-offer-row">
                @if (o.thumbnail) {
                  <img [src]="o.thumbnail" alt="" class="modal-thumb" loading="lazy" />
                } @else {
                  <span class="modal-thumb placeholder" aria-hidden="true">📦</span>
                }
                <div class="modal-offer-body">
                  @if (o.source && o.link) {
                    <a [href]="o.link" target="_blank" rel="noopener" class="modal-brand">{{ o.source }}</a>
                  } @else {
                    <span class="modal-brand-text">{{ o.source || o.title || 'Kaynak' }}</span>
                  }
                  @if (o.title && o.title !== o.source) {
                    <span class="modal-title">{{ o.title }}</span>
                  }
                </div>
                <span class="modal-price">{{ formatPrice(o.price, o.currency) }}</span>
              </li>
            }
          </ul>
        </div>
      </div>
    }
  `,
  styles: [`
    .pb-hero, .pb-results { padding: var(--space-6); margin-bottom: var(--space-4); }
    .pb-nav { margin: 0 0 var(--space-3); font-size: 0.95rem; }
    .pb-nav a { color: #4285f4; text-decoration: none; }
    .pb-nav a:hover { text-decoration: underline; }
    .sep { margin: 0 0.4rem; color: #999; }
    .subtitle { color: var(--color-text-muted, #666); margin-bottom: var(--space-4); max-width: 52rem; }
    .pb-form { display: flex; gap: var(--space-3); flex-wrap: wrap; }
    .pb-form input {
      flex: 1; min-width: 240px; padding: 0.6rem 1rem;
      border: 1px solid var(--color-border, #ccc); border-radius: 6px; font-size: 1rem;
    }
    .error { color: #c0392b; margin-top: var(--space-3); }
    .results-header { display: flex; justify-content: space-between; align-items: flex-start; gap: var(--space-4); flex-wrap: wrap; }
    .status { font-size: 0.85rem; padding: 0.2rem 0.6rem; border-radius: 4px; background: #eee; margin-left: 0.5rem; }
    .status.completed { background: #d4edda; color: #155724; }
    .status.failed { background: #f8d7da; color: #721c24; }
    .status.crawling, .status.comparing, .status.pending { background: #fff3cd; color: #856404; }
    .status.cancelled { background: #e8ecf0; color: #475569; }
    .phase-steps {
      display: flex; align-items: center; gap: 0.75rem; margin: var(--space-3) 0;
      padding: var(--space-3); background: #f8fafc; border-radius: 8px; border: 1px solid #e8ecf0;
    }
    .phase {
      display: flex; align-items: center; gap: 0.5rem; color: #94a3b8; font-size: 0.9rem;
    }
    .phase.active { color: #1d4ed8; font-weight: 600; }
    .phase.done { color: #166534; }
    .phase-num {
      width: 1.5rem; height: 1.5rem; border-radius: 50%; background: #e2e8f0;
      display: inline-flex; align-items: center; justify-content: center; font-size: 0.8rem; font-weight: 700;
    }
    .phase.active .phase-num { background: #dbeafe; color: #1d4ed8; }
    .phase.done .phase-num { background: #d4edda; color: #166534; }
    .phase-arrow { color: #cbd5e1; }
    .pending-cell { font-size: 0.8rem; color: #94a3b8; font-style: italic; }
    .stats { display: flex; gap: var(--space-4); margin: var(--space-4) 0; flex-wrap: wrap; }
    .stat { padding: var(--space-3); background: var(--color-surface-2, #f5f5f5); border-radius: 6px; min-width: 100px; text-align: center; }
    .stat span { display: block; font-size: 0.8rem; color: #666; margin-top: 0.15rem; }
    .stat.below strong { color: #27ae60; }
    .stat.average strong { color: #f39c12; }
    .stat.above strong { color: #e74c3c; }
    .progress { color: #666; font-style: italic; }
    .warn-banner {
      padding: var(--space-3); background: #fff8e6; border: 1px solid #f0d78c;
      border-radius: 6px; font-size: 0.9rem; color: #856404; margin-bottom: var(--space-3);
    }
    .toolbar { display: flex; align-items: center; gap: var(--space-3); margin-bottom: var(--space-3); flex-wrap: wrap; }
    .toolbar select { padding: 0.4rem 0.6rem; border-radius: 6px; border: 1px solid #ccc; }
    .toolbar-meta { font-size: 0.85rem; color: #666; }
    .table-wrap { overflow-x: auto; }
    .pb-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    .pb-table th, .pb-table td { padding: 0.5rem 0.65rem; border-bottom: 1px solid #eee; text-align: left; vertical-align: top; }
    .pb-table th { white-space: nowrap; background: #fafafa; }
    .product-cell { min-width: 180px; max-width: 280px; }
    .product-title { font-weight: 500; color: #1a73e8; text-decoration: none; word-break: break-word; display: block; }
    .product-title:hover { text-decoration: underline; }
    .offer-cell { display: flex; gap: 0.5rem; align-items: flex-start; min-width: 140px; }
    .offer-thumb {
      width: 44px; height: 44px; object-fit: contain; border-radius: 4px;
      background: #f5f5f5; border: 1px solid #eee; flex-shrink: 0;
    }
    .offer-thumb.placeholder {
      display: flex; align-items: center; justify-content: center;
      font-size: 1.1rem; line-height: 44px; text-align: center;
    }
    .offer-meta { display: flex; flex-direction: column; gap: 0.1rem; min-width: 0; }
    .offer-price { font-weight: 600; white-space: nowrap; }
    .offer-brand { font-size: 0.75rem; color: #4285f4; text-decoration: none; word-break: break-word; }
    .offer-brand:hover { text-decoration: underline; }
    .offer-brand-text { font-size: 0.75rem; color: #555; word-break: break-word; }
    .avg-cell { display: flex; flex-direction: column; align-items: flex-start; gap: 0.35rem; }
    .offers-btn {
      font-size: 0.72rem; padding: 0.2rem 0.5rem; border-radius: 4px;
      border: 1px solid #c5d4f7; background: #eef4ff; color: #1a56db;
      cursor: pointer; white-space: nowrap;
    }
    .offers-btn:hover { background: #dbeafe; }
    .modal-backdrop {
      position: fixed; inset: 0; z-index: 1000;
      background: rgba(15, 23, 42, 0.45);
      display: flex; align-items: center; justify-content: center;
      padding: 1rem;
    }
    .modal-panel {
      background: #fff; border-radius: 10px; width: min(520px, 100%);
      max-height: min(80vh, 640px); display: flex; flex-direction: column;
      box-shadow: 0 20px 50px rgba(0,0,0,0.2);
    }
    .modal-header {
      display: flex; justify-content: space-between; align-items: flex-start;
      gap: 0.75rem; padding: 1rem 1rem 0.5rem; border-bottom: 1px solid #eee;
    }
    .modal-header h3 { margin: 0; font-size: 1rem; line-height: 1.35; word-break: break-word; }
    .modal-close {
      border: none; background: transparent; font-size: 1.5rem; line-height: 1;
      cursor: pointer; color: #666; padding: 0 0.25rem;
    }
    .modal-close:hover { color: #111; }
    .modal-sub { margin: 0; padding: 0.5rem 1rem; font-size: 0.8rem; color: #666; }
    .modal-offers {
      list-style: none; margin: 0; padding: 0 0.5rem 0.75rem;
      overflow-y: auto; flex: 1;
    }
    .modal-offer-row {
      display: flex; align-items: center; gap: 0.65rem;
      padding: 0.55rem 0.5rem; border-bottom: 1px solid #f0f0f0;
    }
    .modal-offer-row:last-child { border-bottom: none; }
    .modal-thumb {
      width: 48px; height: 48px; object-fit: contain; border-radius: 4px;
      background: #f8f8f8; border: 1px solid #eee; flex-shrink: 0;
    }
    .modal-thumb.placeholder {
      display: flex; align-items: center; justify-content: center; font-size: 1.2rem;
    }
    .modal-offer-body { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 0.1rem; }
    .modal-brand { font-weight: 600; color: #1a73e8; text-decoration: none; font-size: 0.85rem; }
    .modal-brand:hover { text-decoration: underline; }
    .modal-brand-text { font-weight: 600; font-size: 0.85rem; color: #333; }
    .modal-title { font-size: 0.75rem; color: #666; word-break: break-word; }
    .modal-price { font-weight: 600; white-space: nowrap; font-size: 0.9rem; }
    .shop-err {
      display: block; font-size: 0.72rem; color: #c0392b; margin-top: 0.2rem;
      line-height: 1.3; max-width: 260px;
    }
    .delta-low { color: #27ae60; font-weight: 600; }
    .delta-high { color: #e74c3c; font-weight: 600; }
    .pos-badge { font-size: 0.75rem; padding: 0.15rem 0.45rem; border-radius: 4px; font-weight: 600; white-space: nowrap; }
    .pos-below { background: #d4edda; color: #155724; }
    .pos-average { background: #fff3cd; color: #856404; }
    .pos-above { background: #fdecea; color: #c0392b; }
    .pos-unknown { background: #eee; color: #666; }
    .offer-count { display: block; font-size: 0.7rem; color: #888; margin-top: 0.15rem; }
    .load-more { margin-top: var(--space-4); }
    .empty { color: #666; font-style: italic; }
  `],
})
export class PriceBenchmarkComponent implements OnInit {
  private api = inject(PriceBenchmarkApiService);
  private poll = inject(PriceBenchmarkPollService);
  private destroyRef = inject(DestroyRef);

  urlInput = '';
  loading = signal(false);
  stopping = signal(false);
  loadingMore = signal(false);
  error = signal<string | null>(null);
  detail = signal<PriceBenchmarkDetailDto | null>(null);
  extraProducts = signal<PriceBenchmarkItemDto[]>([]);
  positionFilter = signal('all');
  offersModal = signal<{ productTitle: string; offers: ShoppingOfferView[] } | null>(null);

  protected readonly statusLabel = computed(() => priceBenchmarkStatusLabel(this.detail()?.run.status));
  protected readonly statusClass = computed(() => this.detail()?.run.status.toLowerCase() ?? '');
  protected readonly isRunning = computed(() => isPriceBenchmarkRunning(this.detail()?.run.status));
  protected readonly isDiscovering = computed(() =>
    isDiscoverPhase(this.detail()?.run.status, this.detail()?.run.progressPhase));
  protected readonly isComparing = computed(() =>
    isComparePhase(this.detail()?.run.status, this.detail()?.run.progressPhase));
  protected readonly isCompleted = computed(() => this.detail()?.run.status === 'Completed');

  allProducts = computed(() => {
    const d = this.detail();
    if (!d) return [];
    const seen = new Set<string>();
    const merged: PriceBenchmarkItemDto[] = [];
    for (const p of [...d.products, ...this.extraProducts()]) {
      if (seen.has(p.entityId)) continue;
      seen.add(p.entityId);
      merged.push(p);
    }
    return merged;
  });

  filteredProducts = computed(() => {
    const filter = this.positionFilter();
    const items = this.allProducts();
    if (filter === 'all') return items;
    return items.filter((p) => (p.marketPosition ?? 'unknown').toLowerCase() === filter);
  });

  belowCount = computed(() => this.allProducts().filter((p) => p.marketPosition === 'below').length);
  averageCount = computed(() => this.allProducts().filter((p) => p.marketPosition === 'average').length);
  aboveCount = computed(() => this.allProducts().filter((p) => p.marketPosition === 'above').length);
  comparedCount = computed(() => this.allProducts().filter((p) => isProductCompared(p)).length);
  hasShoppingErrors = computed(() => this.allProducts().some((p) => !!p.shoppingError));

  hasMoreProducts = computed(() => {
    const run = this.detail()?.run;
    if (!run || run.status !== 'Completed') return false;
    return this.allProducts().length < run.totalProducts;
  });

  ngOnInit(): void {
    // no-op — standalone page
  }

  positionLabel(position: string | undefined): string {
    return marketPositionLabel(position);
  }

  positionClass(position: string | undefined): string {
    return marketPositionClass(position);
  }

  productCompared(item: PriceBenchmarkItemDto): boolean {
    return isProductCompared(item);
  }

  shoppingErrorLabel(error: string | null | undefined): string {
    if (!error) return '';
    if (/captcha|engelledi|sorry/i.test(error)) {
      return '⚠ Google CAPTCHA — SERP_API_KEY gerekli';
    }
    if (error.length > 80) return `⚠ ${error.slice(0, 77)}…`;
    return `⚠ ${error}`;
  }

  minOffer(item: PriceBenchmarkItemDto): ShoppingOfferView | null {
    return getMinOffer(item);
  }

  maxOffer(item: PriceBenchmarkItemDto): ShoppingOfferView | null {
    return getMaxOffer(item);
  }

  openOffersModal(item: PriceBenchmarkItemDto): void {
    const offers = parseShoppingOffers(item.shoppingOffersJson);
    if (!offers.length) return;
    this.offersModal.set({
      productTitle: item.title || item.pageUrl,
      offers,
    });
  }

  closeOffersModal(): void {
    this.offersModal.set(null);
  }

  formatPrice(value: number | null | undefined, currency?: string | null): string {
    if (value == null) return '—';
    const cur = currency?.trim() || 'TRY';
    try {
      return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: cur, maximumFractionDigits: 0 }).format(value);
    } catch {
      return `${value.toLocaleString('tr-TR')} ${cur}`;
    }
  }

  onSubmit(event: Event): void {
    event.preventDefault();
    const url = this.urlInput.trim();
    if (!url || this.isRunning()) return;

    this.loading.set(true);
    this.error.set(null);
    this.detail.set(null);
    this.extraProducts.set([]);
    this.positionFilter.set('all');
    this.offersModal.set(null);
    this.poll.close();

    this.api.start(url).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        this.error.set(err?.error?.title ?? err?.error?.errors?.url?.[0] ?? 'Analiz başlatılamadı.');
        this.loading.set(false);
        return of(null);
      }),
    ).subscribe((run) => {
      if (!run) return;
      this.pollRun(run.entityId);
    });
  }

  onCancel(): void {
    const id = this.detail()?.run.entityId;
    if (!id || this.stopping()) return;

    this.stopping.set(true);
    this.api.cancel(id).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        this.error.set(err?.error?.message ?? 'Analiz durdurulamadı.');
        this.stopping.set(false);
        return of(null);
      }),
    ).subscribe((run) => {
      this.stopping.set(false);
      if (!run) return;
      this.poll.close();
      this.api.get(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((d) => this.detail.set(d));
    });
  }

  loadMore(): void {
    const d = this.detail();
    if (!d || this.loadingMore()) return;

    const skip = this.allProducts().length;
    this.loadingMore.set(true);
    this.api.getProducts(d.run.entityId, skip, 50).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => of([] as PriceBenchmarkItemDto[])),
    ).subscribe((rows) => {
      this.extraProducts.update((prev) => [...prev, ...rows]);
      this.loadingMore.set(false);
    });
  }

  private pollRun(entityId: string): void {
    this.poll.watch(
      entityId,
      this.destroyRef,
      (d) => {
        this.detail.set(d);
        this.loading.set(false);
      },
      (msg) => {
        this.error.set(msg);
        this.loading.set(false);
      },
    );
  }
}
