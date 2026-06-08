import { Component, ChangeDetectionStrategy, inject, signal, OnDestroy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { AdminAuditService, AuditLogDto, AuditQueryParams } from '../admin-audit.service';
import { AuditChangesComponent } from './audit-changes.component';
import { NotificationService } from '../notification.service';

/**
 * Admin AuditLog listesi.
 *
 * Özellikler:
 * - Action / Audience / Actor / Tarih filtreleri
 * - Her satırda actor + ip + action + target
 * - ChangesJson varsa expand'leşebilir diff viewer
 * - Pagination (take/skip)
 */
@Component({
  selector: 'SearchConsoleApp-audit-list',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, AuditChangesComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="audit-page">
      <h2>Audit Log</h2>

      <div class="filters">
        <input
          type="text" placeholder="Action (örn: customer.update)"
          [(ngModel)]="filterAction"
          (keyup.enter)="search()"
        />
        <select [(ngModel)]="filterAudience">
          <option value="">Tüm audience'lar</option>
          <option value="public">Public</option>
          <option value="web">Web</option>
          <option value="admin">Admin</option>
          <option value="background">Background</option>
        </select>
        <input
          type="number" placeholder="Actor Customer Id"
          [(ngModel)]="filterActorCustomerId"
        />
        <input
          type="date" placeholder="Başlangıç"
          [(ngModel)]="filterFrom"
        />
        <input
          type="date" placeholder="Bitiş"
          [(ngModel)]="filterTo"
        />
        <button (click)="search()">Filtrele</button>
        <button (click)="clear()">Temizle</button>
      </div>

      @if (loading()) {
        <p class="muted">Yükleniyor…</p>
      } @else if (error()) {
        <p class="error">{{ error() }}</p>
      } @else if (logs().length === 0) {
        <p class="muted">Kayıt yok.</p>
      } @else {
        <table class="log-table">
          <thead>
            <tr>
              <th>Zaman</th>
              <th>Audience</th>
              <th>Actor</th>
              <th>Action</th>
              <th>Target</th>
              <th>Outcome</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (log of logs(); track log.id) {
              <tr class="log-row" [class.failure]="log.outcome === 'failure'">
                <td>{{ log.timestamp | date: 'yyyy-MM-dd HH:mm:ss' }}</td>
                <td><span class="badge">{{ log.audience }}</span></td>
                <td>
                  @if (log.actorEmail) {
                    <div>{{ log.actorEmail }}</div>
                    <div class="meta">{{ log.actorIp }}</div>
                  } @else {
                    <span class="muted">—</span>
                  }
                </td>
                <td><code>{{ log.action }}</code></td>
                <td>
                  @if (log.targetType) {
                    <code>{{ log.targetType }}/{{ log.targetId }}</code>
                  } @else {
                    <span class="muted">—</span>
                  }
                </td>
                <td>
                  <span class="outcome" [class.success]="log.outcome === 'success'" [class.failure]="log.outcome === 'failure'">
                    {{ log.outcome }}
                  </span>
                  @if (log.failureReason) {
                    <div class="meta">{{ log.failureReason }}</div>
                  }
                </td>
                <td>
                  @if (log.changesJson) {
                    <button class="btn-link" (click)="toggle(log.id)">
                      {{ expanded() === log.id ? 'Gizle' : 'Detay' }}
                    </button>
                  }
                </td>
              </tr>
              @if (expanded() === log.id && log.changesJson) {
                <tr class="expanded-row">
                  <td colspan="7">
                    <SearchConsoleApp-audit-changes [json]="log.changesJson" />
                  </td>
                </tr>
              }
            }
          </tbody>
        </table>

        <div class="pagination">
          <button (click)="prevPage()" [disabled]="skip() === 0">‹ Önceki</button>
          <span class="page-info">{{ skip() + 1 }} – {{ skip() + logs().length }}</span>
          <button (click)="nextPage()" [disabled]="logs().length < take">Sonraki ›</button>
        </div>
      }
    </div>
  `,
  styles: [`
    .audit-page { padding: 1rem; max-width: 1200px; margin: 0 auto; }
    .filters {
      display: flex; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 1rem;
      align-items: center;
    }
    .filters input, .filters select {
      padding: 0.4rem 0.6rem; border: 1px solid var(--color-border);
      border-radius: var(--radius-sm); background: var(--color-background);
      color: var(--color-text);
    }
    .filters button {
      padding: 0.4rem 0.8rem; background: var(--color-primary);
      color: var(--color-primary-foreground); border: none;
      border-radius: var(--radius-sm); cursor: pointer;
    }
    .log-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    .log-table th { text-align: left; padding: 0.5rem; color: var(--color-text-muted); font-weight: 500; border-bottom: 2px solid var(--color-border); }
    .log-table td { padding: 0.5rem; border-bottom: 1px solid var(--color-border); vertical-align: top; }
    .log-row.failure { background: color-mix(in srgb, var(--color-danger) 8%, transparent); }
    .meta { font-size: 0.75rem; color: var(--color-text-subtle); margin-top: 2px; }
    .badge { padding: 2px 6px; background: var(--color-surface); border-radius: 99px; font-size: 0.75rem; }
    code { font-family: ui-monospace, monospace; font-size: 0.85em; }
    .outcome.success { color: var(--color-success); }
    .outcome.failure { color: var(--color-danger); font-weight: 500; }
    .btn-link { background: none; border: none; color: var(--color-primary); cursor: pointer; padding: 0; text-decoration: underline; }
    .muted { color: var(--color-text-muted); }
    .error { color: var(--color-danger); }
    .expanded-row td { background: var(--color-surface); padding: 1rem; }
    .pagination { display: flex; gap: 1rem; align-items: center; justify-content: center; margin-top: 1rem; }
    .pagination button {
      padding: 0.4rem 1rem; border: 1px solid var(--color-border);
      background: var(--color-background); color: var(--color-text);
      border-radius: var(--radius-sm); cursor: pointer;
    }
    .pagination button:disabled { opacity: 0.4; cursor: not-allowed; }
    .page-info { color: var(--color-text-muted); font-size: 0.85rem; }
  `]
})
export class AuditLogListComponent implements OnDestroy {
  private auditService = inject(AdminAuditService);
  private notifications = inject(NotificationService);
  private liveSub?: Subscription;

  readonly take = 50;
  readonly skip = signal(0);
  readonly logs = signal<AuditLogDto[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly expanded = signal<number | null>(null);
  readonly liveEnabled = signal(true);   // toggle: canlı feed açık/kapalı

  // Filtreler — ngModel ile two-way binding
  filterAction = '';
  filterAudience = '';
  filterActorCustomerId: number | null = null;
  filterFrom = '';
  filterTo = '';

  ngOnInit(): void {
    this.load();

    // Canlı feed: yeni audit event geldiğinde listenin başına ekle.
    // Filtre aktifse ve event filtreye uymuyorsa ekleme.
    this.liveSub = this.notifications.auditEvent$.subscribe(event => {
      if (!this.liveEnabled()) return;
      if (this.skip() > 0) return;   // ilk sayfada değilsek karıştırma

      // Filtre kontrolü — basit match
      if (this.filterAction && event.action !== this.filterAction) return;
      if (this.filterAudience && event.audience !== this.filterAudience) return;
      if (this.filterActorCustomerId && event.actorCustomerId !== this.filterActorCustomerId) return;

      this.logs.update(current => [event as AuditLogDto, ...current].slice(0, this.take));
    });

    // Hub'a bağlan (idempotent)
    this.notifications.connect();
  }

  ngOnDestroy(): void {
    this.liveSub?.unsubscribe();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(null);

    const params: AuditQueryParams = {
      take: this.take,
      skip: this.skip(),
    };
    if (this.filterAction) params.action = this.filterAction;
    if (this.filterAudience) params.audience = this.filterAudience;
    if (this.filterActorCustomerId) params.actorCustomerId = this.filterActorCustomerId;
    if (this.filterFrom) params.from = this.filterFrom;
    if (this.filterTo) params.to = this.filterTo;

    this.auditService.query(params).subscribe({
      next: (list) => {
        this.logs.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Audit log yüklenemedi.');
        this.loading.set(false);
      }
    });
  }

  search(): void {
    this.skip.set(0);
    this.load();
  }

  clear(): void {
    this.filterAction = '';
    this.filterAudience = '';
    this.filterActorCustomerId = null;
    this.filterFrom = '';
    this.filterTo = '';
    this.skip.set(0);
    this.load();
  }

  toggle(id: number): void {
    this.expanded.set(this.expanded() === id ? null : id);
  }

  nextPage(): void {
    this.skip.update(s => s + this.take);
    this.load();
  }

  prevPage(): void {
    this.skip.update(s => Math.max(0, s - this.take));
    this.load();
  }
}
