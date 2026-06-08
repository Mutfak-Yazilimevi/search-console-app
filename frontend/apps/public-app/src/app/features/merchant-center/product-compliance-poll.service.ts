import { DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval, Subscription } from 'rxjs';
import { startWith, switchMap, takeWhile } from 'rxjs/operators';
import { ProductComplianceApiService } from './product-compliance-api.service';
import { ProductComplianceDetailDto } from './merchant-center.models';

export interface CompliancePollCallbacks {
  onUpdate: (detail: ProductComplianceDetailDto, previous: ProductComplianceDetailDto | null) => void;
  onError: (message: string) => void;
}

@Injectable({ providedIn: 'root' })
export class ProductCompliancePollService {
  private complianceApi = inject(ProductComplianceApiService);
  private pollSub: Subscription | null = null;

  watch(
    entityId: string,
    destroyRef: DestroyRef,
    callbacks: CompliancePollCallbacks,
    getPrevious: () => ProductComplianceDetailDto | null,
  ): void {
    this.close();
    let previous = getPrevious();

    this.pollSub = interval(2000).pipe(
      startWith(0),
      switchMap(() => this.complianceApi.get(entityId)),
      takeWhile((d) => {
        if (d.run.progressPhase === 'rescanning') return true;
        return !['Completed', 'Failed', 'Cancelled'].includes(d.run.status);
      }, true),
      takeUntilDestroyed(destroyRef),
    ).subscribe({
      next: (d) => {
        callbacks.onUpdate(d, previous);
        previous = d;
      },
      error: () => callbacks.onError('Sonuçlar alınamadı.'),
    });
  }

  close(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = null;
  }
}

@Injectable({ providedIn: 'root' })
export class MerchantCenterRecentRunsService {
  private static readonly LocalRecentKey = 'gmc-recent-runs';
  private complianceApi = inject(ProductComplianceApiService);

  loadAuthenticated(limit = 8) {
    return this.complianceApi.listRecentRuns(limit);
  }

  loadLocal(): import('./merchant-center.models').ProductComplianceRunSummaryDto[] {
    try {
      const raw = localStorage.getItem(MerchantCenterRecentRunsService.LocalRecentKey);
      if (!raw) return [];
      const parsed = JSON.parse(raw) as import('./merchant-center.models').ProductComplianceRunSummaryDto[];
      return Array.isArray(parsed) ? parsed.slice(0, 8) : [];
    } catch {
      return [];
    }
  }

  trackLocal(run: import('./merchant-center.models').ProductComplianceRunSummaryDto): void {
    const entry = {
      entityId: run.entityId,
      inputUrl: run.inputUrl,
      status: run.status,
      analysisMode: run.analysisMode,
      createdAt: run.createdAt,
      completedAt: run.completedAt ?? null,
      complianceScore: run.complianceScore ?? null,
      totalProducts: run.totalProducts ?? 0,
    };
    const next = [entry, ...this.loadLocal().filter((r) => r.entityId !== entry.entityId)].slice(0, 8);
    localStorage.setItem(MerchantCenterRecentRunsService.LocalRecentKey, JSON.stringify(next));
  }
}
