import { DestroyRef, inject, Injectable } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval, Subscription } from 'rxjs';
import { startWith, switchMap } from 'rxjs/operators';
import { AuditApiService } from './audit-api.service';
import type { AuditDetailDto } from './audit.models';

@Injectable({ providedIn: 'root' })
export class AuditPollService {
  private auditApi = inject(AuditApiService);
  private eventSource: EventSource | null = null;
  private pollSub: Subscription | null = null;

  watch(
    entityId: string,
    destroyRef: DestroyRef,
    onUpdate: (detail: AuditDetailDto) => void,
    onError: (message: string) => void,
  ): void {
    this.close();
    this.startPolling(entityId, destroyRef, onUpdate, onError);

    try {
      this.eventSource = new EventSource(this.auditApi.eventsUrl(entityId));
      this.eventSource.onmessage = () => {
        this.fetchOnce(entityId, destroyRef, onUpdate, onError);
      };
      this.eventSource.onerror = () => {
        this.eventSource?.close();
        this.eventSource = null;
      };
    } catch {
      // interval polling already running
    }
  }

  close(): void {
    this.eventSource?.close();
    this.eventSource = null;
    this.pollSub?.unsubscribe();
    this.pollSub = null;
  }

  private startPolling(
    entityId: string,
    destroyRef: DestroyRef,
    onUpdate: (detail: AuditDetailDto) => void,
    onError: (message: string) => void,
  ): void {
    this.pollSub = interval(2000)
      .pipe(
        startWith(0),
        switchMap(() => this.auditApi.get(entityId)),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe({
        next: (d) => {
          onUpdate(d);
          if (d.run.status === 'Completed' || d.run.status === 'Failed' || d.run.status === 'Cancelled') {
            this.close();
          }
        },
        error: () => onError('Sonuçlar alınamadı.'),
      });
  }

  private fetchOnce(
    entityId: string,
    destroyRef: DestroyRef,
    onUpdate: (detail: AuditDetailDto) => void,
    onError: (message: string) => void,
  ): void {
    this.auditApi.get(entityId).pipe(takeUntilDestroyed(destroyRef)).subscribe({
      next: onUpdate,
      error: () => onError('Sonuçlar alınamadı.'),
    });
  }
}
