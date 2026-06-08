import { Component, ChangeDetectionStrategy, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { SessionsService } from '../sessions.service';
import { SessionDto } from '@SearchConsoleApp/shared/models';

/**
 * Kullanıcı "Oturumlarım" sayfası.
 * Aktif oturumları gösterir, cihaz/IP/zaman bilgisiyle.
 * "Bu oturumu kapat" + "Diğerlerinden çık" aksiyonları.
 *
 * Bu komponent shared library'de — hem web-app hem admin-app tüketir.
 */
@Component({
  selector: 'SearchConsoleApp-sessions-list',
  standalone: true,
  imports: [CommonModule, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sessions-page">
      <header>
        <h2>Aktif Oturumlar</h2>
        <button
          class="btn-danger"
          (click)="revokeOthers()"
          [disabled]="loading() || sessions().length <= 1"
        >
          Diğer Oturumlardan Çık
        </button>
      </header>

      @if (loading()) {
        <p class="muted">Yükleniyor…</p>
      } @else if (error()) {
        <p class="error">{{ error() }}</p>
      } @else if (sessions().length === 0) {
        <p class="muted">Aktif oturum yok.</p>
      } @else {
        <ul class="session-list">
          @for (s of sessions(); track s.id) {
            <li class="session-row" [class.current]="s.isCurrent">
              <div class="info">
                <div class="device-line">
                  <span class="ua">{{ shortenUa(s.userAgent) }}</span>
                  @if (s.isCurrent) {
                    <span class="badge badge-current">Bu cihaz</span>
                  }
                  <span class="badge badge-audience">{{ s.audience }}</span>
                </div>
                <div class="meta">
                  <span>{{ s.ipAddress }}</span>
                  @if (s.ipCountry) {
                    <span>· {{ s.ipCountry }}</span>
                  }
                  <span>· Başladı: {{ s.startedUtc | date: 'short' }}</span>
                  <span>· Son aktivite: {{ s.lastActivityUtc | date: 'short' }}</span>
                </div>
              </div>
              @if (!s.isCurrent) {
                <button class="btn-link-danger" (click)="revoke(s.id)">
                  Bu oturumu kapat
                </button>
              }
            </li>
          }
        </ul>
      }
    </div>
  `,
  styles: [`
    .sessions-page { padding: 1rem; max-width: 800px; margin: 0 auto; }
    header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; }
    .session-list { list-style: none; padding: 0; margin: 0; }
    .session-row {
      display: flex; justify-content: space-between; align-items: flex-start;
      padding: 1rem; border: 1px solid var(--color-border); border-radius: var(--radius-md);
      margin-bottom: 0.5rem;
    }
    .session-row.current { border-color: var(--color-primary); background: var(--color-surface); }
    .device-line { font-weight: 500; display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; }
    .ua { color: var(--color-text); }
    .meta { font-size: 0.85rem; color: var(--color-text-muted); margin-top: 0.25rem; }
    .meta > span { margin-right: 0.25rem; }
    .badge { font-size: 0.7rem; padding: 0.15rem 0.5rem; border-radius: 999px; }
    .badge-current { background: var(--color-primary); color: var(--color-primary-foreground); }
    .badge-audience { background: var(--color-surface); border: 1px solid var(--color-border); color: var(--color-text-muted); }
    .btn-danger {
      background: var(--color-danger); color: white; border: none;
      padding: 0.5rem 1rem; border-radius: var(--radius-md); cursor: pointer;
    }
    .btn-danger:disabled { opacity: 0.5; cursor: not-allowed; }
    .btn-link-danger {
      background: none; color: var(--color-danger); border: none;
      cursor: pointer; padding: 0; text-decoration: underline;
    }
    .muted { color: var(--color-text-muted); }
    .error { color: var(--color-danger); }
  `]
})
export class SessionsListComponent {
  private sessionsService = inject(SessionsService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly sessions = signal<SessionDto[]>([]);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.error.set(null);
    this.sessionsService.listActive().subscribe({
      next: (list) => {
        this.sessions.set(list);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Oturumlar yüklenemedi.');
        this.loading.set(false);
      }
    });
  }

  revoke(sessionId: number): void {
    if (!confirm('Bu oturumu kapatmak istiyor musunuz?')) return;
    this.sessionsService.revoke(sessionId).subscribe({
      next: () => this.refresh(),
      error: () => this.error.set('Oturum kapatılamadı.')
    });
  }

  revokeOthers(): void {
    if (!confirm('Diğer tüm cihazlardaki oturumlar kapatılacak. Devam edilsin mi?')) return;
    this.sessionsService.revokeOthers().subscribe({
      next: () => this.refresh(),
      error: () => this.error.set('Oturumlar kapatılamadı.')
    });
  }

  /** Uzun UA string'lerini kısalt (browser + OS info). */
  shortenUa(ua: string | undefined): string {
    if (!ua) return 'Bilinmeyen cihaz';
    // Çok kaba bir özet — production'da ua-parser-js kullan
    const browser = /(?:Chrome|Firefox|Safari|Edge|Opera)\/[\d.]+/.exec(ua)?.[0];
    const os = /(?:Windows|Macintosh|Linux|Android|iPhone|iPad)/.exec(ua)?.[0];
    return [browser, os].filter(Boolean).join(' · ') || ua.substring(0, 60);
  }
}
