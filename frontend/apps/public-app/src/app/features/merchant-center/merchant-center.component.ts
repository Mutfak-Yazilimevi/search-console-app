import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
  computed,
  DestroyRef,
  OnInit,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '@SearchConsoleApp/shared/core';
import { ProductComplianceApiService } from './product-compliance-api.service';
import { MerchantCenterApiService } from './merchant-center-api.service';
import { AuditApiService, IntegrationItemDto } from '../audit/audit-api.service';
import { filterGmcIntegrations } from './gmc-integration-ids';
import { OAuthSetupGuide, oauthErrorMessage, parseOAuthSetupError } from '../auth/oauth-setup.models';
import {
  MerchantCenterRecentRunsService,
  ProductCompliancePollService,
} from './product-compliance-poll.service';
import {
  comparisonBannerClass as resolveComparisonBannerClass,
  isComplianceRunActive,
} from './gmc-labels';
import { buildComplianceChecklistMarkdown, downloadBlob } from './gmc-compliance-export.utils';
import { MerchantCenterHeroComponent } from './merchant-center-hero.component';
import { GmcRunOverviewComponent } from './gmc-run-overview.component';
import { GmcRunCompletedComponent } from './gmc-run-completed.component';
import {
  GmcBulkAiItemDto,
  MerchantCenterAccountDto,
  ProductComplianceDetailDto,
  ProductComplianceItemDto,
  ProductComplianceProductDetailDto,
  ProductComplianceRunSummaryDto,
} from './merchant-center.models';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'SearchConsoleApp-merchant-center',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MerchantCenterHeroComponent,
    GmcRunOverviewComponent,
    GmcRunCompletedComponent,
  ],
  template: `
    <SearchConsoleApp-merchant-center-hero
      [integrationItems]="integrationItems()"
      [url]="url"
      (urlChange)="url = $event"
      [selectedAccountId]="selectedAccountId"
      (selectedAccountIdChange)="selectedAccountId = $event"
      [gmcConnected]="gmcConnected()"
      [gmcAccounts]="gmcAccounts()"
      [loading]="loading()"
      [error]="error()"
      [oauthGuide]="oauthGuide()"
      [recentRuns]="recentRuns()"
      (start)="onStart()"
      (connectGmc)="connectGmc()"
      (disconnectGmc)="disconnectGmc()"
      (openRun)="openRun($event)"
      (integrationsChanged)="loadIntegrations()" />

    @if (detail()) {
      <SearchConsoleApp-gmc-run-overview
        [run]="detail()!.run"
        [isRunning]="isRunning()"
        [gmcWarningMessage]="gmcWarningMessage()"
        [comparisonBannerClass]="comparisonBannerClass()"
        (cancel)="onCancel()" />

      @if (detail()!.run.status === 'Completed') {
        <SearchConsoleApp-gmc-run-completed
          [detail]="detail()!"
          [products]="filteredProducts()"
          [productDetail]="productDetail()"
          [aiEnabled]="aiEnabled()"
          [statusFilter]="statusFilter"
          [hasMoreProducts]="hasMoreProducts()"
          [loadingMore]="loadingMore()"
          [bulkAiLoading]="bulkAiLoading()"
          [bulkAiResults]="bulkAiResults()"
          [rescanLoading]="rescanLoading()"
          (statusFilterChange)="statusFilter = $event"
          (loadMore)="loadMoreProducts()"
          (selectProduct)="loadProduct($event)"
          (exportJson)="exportReport()"
          (exportChecklist)="exportChecklist()"
          (exportHtml)="exportHtmlReport()"
          (bulkAi)="bulkGenerateTitles()"
          (rescanProduct)="rescanProduct()" />
      }
    }
  `,
})
export class MerchantCenterComponent implements OnInit {
  private complianceApi = inject(ProductComplianceApiService);
  private auditApi = inject(AuditApiService);
  private gmcApi = inject(MerchantCenterApiService);
  private pollService = inject(ProductCompliancePollService);
  private recentRunsService = inject(MerchantCenterRecentRunsService);
  private destroyRef = inject(DestroyRef);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private completedPanel = viewChild(GmcRunCompletedComponent);

  url = '';
  selectedAccountId = '';
  loading = signal(false);
  error = signal<string | null>(null);
  detail = signal<ProductComplianceDetailDto | null>(null);
  productDetail = signal<ProductComplianceProductDetailDto | null>(null);
  gmcConnected = signal(false);
  gmcAccounts = signal<MerchantCenterAccountDto[]>([]);
  oauthGuide = signal<OAuthSetupGuide | null>(null);
  statusFilter = 'all';
  extraProducts = signal<ProductComplianceItemDto[]>([]);
  loadingMore = signal(false);
  bulkAiLoading = signal(false);
  bulkAiResults = signal<GmcBulkAiItemDto[]>([]);
  rescanLoading = signal(false);
  integrationItems = signal<IntegrationItemDto[]>([]);
  recentRuns = signal<ProductComplianceRunSummaryDto[]>([]);
  private pollEntityId = signal<string | null>(null);

  isRunning = computed(() => {
    const run = this.detail()?.run;
    if (!run) return false;
    return isComplianceRunActive(run.status, run.progressPhase);
  });

  comparisonBannerClass = computed(() =>
    resolveComparisonBannerClass(this.detail()?.run.comparison?.complianceScoreDelta ?? 0),
  );

  aiEnabled = computed(() => {
    const gemini = this.integrationItems().find((i) => i.id === 'gemini');
    return gemini?.enabled !== false && gemini?.status === 'configured';
  });

  gmcWarningMessage = computed(() => {
    const issues = this.detail()?.feedIssues ?? [];
    return issues.find((i) => i.ruleId === 'GMC-WARN-001')?.message ?? null;
  });

  allProducts = computed(() => {
    const d = this.detail();
    if (!d) return [];
    const seen = new Set(d.products.map((p) => p.entityId));
    const extra = this.extraProducts().filter((p) => !seen.has(p.entityId));
    return [...d.products, ...extra];
  });

  filteredProducts = computed(() => {
    if (this.statusFilter === 'all') return this.allProducts();
    return this.allProducts().filter((p) => p.status === this.statusFilter);
  });

  hasMoreProducts = computed(() => {
    const d = this.detail();
    if (!d) return false;
    return this.allProducts().length < d.run.totalProducts;
  });

  ngOnInit(): void {
    this.loadIntegrations();

    this.route.queryParamMap.pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe((params) => {
      const runId = params.get('run');
      if (runId && runId !== this.detail()?.run.entityId && runId !== this.pollEntityId()) {
        this.loadExistingRun(runId);
      }
    });

    if (this.auth.isAuthenticated()) {
      this.gmcApi.getStatus().pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(() => of({ connected: false, accounts: [] })),
      ).subscribe((s) => {
        this.gmcConnected.set(s.connected);
        this.gmcAccounts.set(s.accounts);
      });

      this.recentRunsService.loadAuthenticated(8).pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(() => of([] as ProductComplianceRunSummaryDto[])),
      ).subscribe((runs) => this.recentRuns.set(runs));
    } else {
      this.recentRuns.set(this.recentRunsService.loadLocal());
    }
  }

  private auth = inject(AuthService);

  loadIntegrations(): void {
    this.auditApi.getIntegrationStatus().pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => of({ integrations: [] as IntegrationItemDto[] })),
    ).subscribe((r) => this.integrationItems.set(filterGmcIntegrations(r.integrations)));
  }

  openRun(entityId: string): void {
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { run: entityId },
      queryParamsHandling: 'merge',
    });
    this.loadExistingRun(entityId);
  }

  private loadExistingRun(entityId: string): void {
    this.productDetail.set(null);
    this.extraProducts.set([]);
    this.complianceApi.get(entityId).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => {
        this.error.set('Analiz bulunamadı.');
        return of(null);
      }),
    ).subscribe((d) => {
      if (!d) return;
      this.detail.set(d);
      this.url = d.run.inputUrl;
      const running = d.run.progressPhase === 'rescanning'
        || d.run.status === 'Pending'
        || d.run.status === 'Crawling'
        || d.run.status === 'Analyzing';
      if (running) {
        this.startPolling(entityId);
      }
    });
  }

  onStart(): void {
    if (!this.url.trim()) return;
    this.loading.set(true);
    this.error.set(null);
    this.productDetail.set(null);
    this.extraProducts.set([]);
    this.statusFilter = 'all';

    const req = this.auth.isAuthenticated() && this.gmcConnected()
      ? this.complianceApi.startConnected(
          this.url.trim(),
          this.selectedAccountId || undefined,
        )
      : this.complianceApi.start(this.url.trim());

    req.pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => {
        this.error.set('Analiz başlatılamadı.');
        this.loading.set(false);
        return of(null);
      }),
    ).subscribe((run) => {
      this.loading.set(false);
      if (!run) return;
      this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { run: run.entityId },
        queryParamsHandling: 'merge',
        replaceUrl: true,
      });
      this.pollEntityId.set(run.entityId);
      this.startPolling(run.entityId);
      if (!this.auth.isAuthenticated()) {
        this.recentRunsService.trackLocal({
          entityId: run.entityId,
          inputUrl: run.inputUrl,
          status: run.status,
          analysisMode: run.analysisMode,
          createdAt: run.createdAt,
          completedAt: run.completedAt,
          complianceScore: run.complianceScore,
          totalProducts: run.totalProducts ?? 0,
        });
        this.recentRuns.set(this.recentRunsService.loadLocal());
      }
    });
  }

  connectGmc(): void {
    this.oauthGuide.set(null);
    this.gmcApi.getAuthorizeUrl(window.location.origin + '/auth/merchant-center/callback').pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((err) => {
        const guide = parseOAuthSetupError(err);
        if (guide) this.oauthGuide.set(guide);
        else this.error.set(oauthErrorMessage(err, 'GMC bağlantısı başlatılamadı.'));
        return of(null);
      }),
    ).subscribe((r) => {
      if (r?.authorizeUrl) window.location.href = r.authorizeUrl;
    });
  }

  onCancel(): void {
    const id = this.detail()?.run.entityId ?? this.pollEntityId();
    if (!id) return;
    this.complianceApi.cancel(id).pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(() => {
      this.complianceApi.get(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe((d) => this.detail.set(d));
    });
  }

  disconnectGmc(): void {
    this.gmcApi.disconnect().pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => of(null)),
    ).subscribe(() => {
      this.gmcConnected.set(false);
      this.gmcAccounts.set([]);
      this.selectedAccountId = '';
    });
  }

  exportReport(): void {
    const id = this.detail()?.run.entityId;
    if (!id) return;
    this.complianceApi.exportJson(id).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => {
        this.error.set('Rapor indirilemedi.');
        return of(null);
      }),
    ).subscribe((data) => {
      if (!data) return;
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      downloadBlob(blob, `gmc-compliance-${id}.json`);
    });
  }

  exportChecklist(): void {
    const d = this.detail();
    if (!d) return;
    const blob = new Blob(
      [buildComplianceChecklistMarkdown(d)],
      { type: 'text/markdown;charset=utf-8' },
    );
    downloadBlob(blob, `gmc-checklist-${d.run.entityId}.md`);
  }

  exportHtmlReport(): void {
    const id = this.detail()?.run.entityId;
    if (!id) return;
    window.open(this.complianceApi.exportHtmlUrl(id), '_blank', 'noopener');
  }

  bulkGenerateTitles(): void {
    const id = this.detail()?.run.entityId;
    if (!id) return;
    this.bulkAiLoading.set(true);
    this.bulkAiResults.set([]);
    this.complianceApi.aiBulkGenerate(id, 'title').pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((e) => {
        this.error.set(e?.error?.message ?? 'Toplu AI başarısız.');
        return of([] as GmcBulkAiItemDto[]);
      }),
    ).subscribe((results) => {
      this.bulkAiLoading.set(false);
      this.bulkAiResults.set(results);
    });
  }

  rescanProduct(): void {
    const runId = this.detail()?.run.entityId;
    const productId = this.productDetail()?.product.entityId;
    if (!runId || !productId) return;
    this.rescanLoading.set(true);
    this.complianceApi.rescanProduct(runId, productId).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((e) => {
        this.error.set(e?.error?.message ?? 'Yeniden tarama başlatılamadı.');
        this.rescanLoading.set(false);
        return of(null);
      }),
    ).subscribe((r) => {
      if (!r) return;
      this.startPolling(runId);
    });
  }

  loadMoreProducts(): void {
    const d = this.detail();
    if (!d || this.loadingMore()) return;
    this.loadingMore.set(true);
    this.complianceApi.getProducts(d.run.entityId, this.allProducts().length, 50).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(() => of([] as ProductComplianceItemDto[])),
    ).subscribe((products) => {
      this.extraProducts.update((cur) => [...cur, ...products]);
      this.loadingMore.set(false);
    });
  }

  loadProduct(productId: string): void {
    const runId = this.detail()?.run.entityId;
    if (!runId) return;
    this.completedPanel()?.resetProductFilters();
    this.complianceApi.getProduct(runId, productId).pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe((d) => this.productDetail.set(d));
  }

  private startPolling(entityId: string): void {
    this.pollEntityId.set(entityId);
    this.pollService.watch(
      entityId,
      this.destroyRef,
      {
        onUpdate: (d, previous) => {
          const wasRescanning = previous?.run.progressPhase === 'rescanning';
          const prevStatus = previous?.run.status;
          this.detail.set(d);
          if (wasRescanning && d.run.progressPhase !== 'rescanning') {
            this.rescanLoading.set(false);
            const productId = this.productDetail()?.product.entityId;
            if (productId) this.loadProduct(productId);
          }
          if (this.auth.isAuthenticated()
              && prevStatus !== d.run.status
              && ['Completed', 'Failed', 'Cancelled'].includes(d.run.status)) {
            this.recentRunsService.loadAuthenticated(8).pipe(
              takeUntilDestroyed(this.destroyRef),
              catchError(() => of([] as ProductComplianceRunSummaryDto[])),
            ).subscribe((runs) => this.recentRuns.set(runs));
          } else if (!this.auth.isAuthenticated()
              && prevStatus !== d.run.status
              && ['Completed', 'Failed'].includes(d.run.status)) {
            this.recentRunsService.trackLocal(d.run);
            this.recentRuns.set(this.recentRunsService.loadLocal());
          }
        },
        onError: (message) => this.error.set(message),
      },
      () => this.detail(),
    );
  }
}
