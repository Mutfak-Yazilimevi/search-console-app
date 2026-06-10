import { DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval, Subscription } from 'rxjs';
import { startWith, switchMap, takeWhile } from 'rxjs/operators';
import { PriceBenchmarkApiService } from './price-benchmark-api.service';
import { PriceBenchmarkDetailDto } from './price-benchmark.models';

@Injectable({ providedIn: 'root' })
export class PriceBenchmarkPollService {
  private api = inject(PriceBenchmarkApiService);
  private pollSub: Subscription | null = null;

  watch(
    entityId: string,
    destroyRef: DestroyRef,
    onUpdate: (detail: PriceBenchmarkDetailDto) => void,
    onError: (message: string) => void,
  ): void {
    this.close();

    this.pollSub = interval(2000).pipe(
      startWith(0),
      switchMap(() => this.api.get(entityId)),
      takeWhile((d) => !['Completed', 'Failed', 'Cancelled'].includes(d.run.status), true),
      takeUntilDestroyed(destroyRef),
    ).subscribe({
      next: onUpdate,
      error: () => onError('Sonuçlar alınamadı.'),
    });
  }

  close(): void {
    this.pollSub?.unsubscribe();
    this.pollSub = null;
  }
}
