import { Injectable, signal, computed, DOCUMENT, inject } from '@angular/core';
import { Theme } from './theme.model';

const STORAGE_KEY = 'SearchConsoleApp.theme';

/**
 * Aktif temayı yönetir. Signal-based.
 * `apply()` çağrıldığında CSS custom property'lerini :root'a yazar.
 * Tüm uygulama anında etkilenir — tüm bileşenler var(--color-primary) okuyarak.
 *
 * Tema kaydı localStorage'da → reload sonrası korunur.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private document = inject(DOCUMENT);

  private _current = signal<Theme | null>(null);
  readonly current = this._current.asReadonly();
  readonly mode = computed(() => this._current()?.mode ?? 'light');

  /**
   * Temayı uygular: :root'a CSS custom property'leri yazar,
   * color-scheme set eder, localStorage'a kaydeder.
   */
  apply(theme: Theme, persist = true): void {
    const root = this.document.documentElement;

    // color-scheme — browser native (form, scrollbar) renkleri için
    root.style.colorScheme = theme.mode;

    // Renkler
    for (const [key, value] of Object.entries(theme.colors)) {
      root.style.setProperty(`--color-${this.kebab(key)}`, value);
    }

    // Fonts
    if (theme.fonts?.sans) root.style.setProperty('--font-sans', theme.fonts.sans);
    if (theme.fonts?.mono) root.style.setProperty('--font-mono', theme.fonts.mono);

    // Radius
    if (theme.radius?.sm) root.style.setProperty('--radius-sm', theme.radius.sm);
    if (theme.radius?.md) root.style.setProperty('--radius-md', theme.radius.md);
    if (theme.radius?.lg) root.style.setProperty('--radius-lg', theme.radius.lg);
    if (theme.radius?.full) root.style.setProperty('--radius-full', theme.radius.full);

    // Shadow
    if (theme.shadow?.sm) root.style.setProperty('--shadow-sm', theme.shadow.sm);
    if (theme.shadow?.md) root.style.setProperty('--shadow-md', theme.shadow.md);
    if (theme.shadow?.lg) root.style.setProperty('--shadow-lg', theme.shadow.lg);

    // Ek
    if (theme.extras) {
      for (const [key, value] of Object.entries(theme.extras)) {
        root.style.setProperty(`--${this.kebab(key)}`, value);
      }
    }

    // data-theme attribute — CSS'te [data-theme="dark"] ile koşullama için
    root.setAttribute('data-theme', theme.name);
    root.setAttribute('data-theme-mode', theme.mode);

    this._current.set(theme);

    if (persist && typeof localStorage !== 'undefined') {
      localStorage.setItem(STORAGE_KEY, theme.name);
    }
  }

  /** Reload sonrası persist edilen tema adını döndürür (yoksa null). */
  loadPersistedThemeName(): string | null {
    if (typeof localStorage === 'undefined') return null;
    return localStorage.getItem(STORAGE_KEY);
  }

  /** Persist edilen seçimi sil — tekrar default'a düşmek için. */
  clearPersisted(): void {
    if (typeof localStorage !== 'undefined') localStorage.removeItem(STORAGE_KEY);
  }

  /** primaryHover → primary-hover gibi CSS var dönüşümü */
  private kebab(s: string): string {
    return s.replace(/[A-Z]/g, m => '-' + m.toLowerCase());
  }
}
