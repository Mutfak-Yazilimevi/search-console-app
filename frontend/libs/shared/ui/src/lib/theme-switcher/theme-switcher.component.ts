import { Component, inject, signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { ThemeService, ThemeLoaderService, BUILT_IN_THEMES } from '@SearchConsoleApp/shared/theme';

interface ThemeOption {
  name: string;
  displayName: string;
  mode: string;
}

/**
 * Kullanıcının runtime'da tema değiştirebileceği dropdown/listele.
 * Built-in + assets'teki + (varsa) backend'deki temaları listeler.
 *
 * Header'a yerleştir; kullanıcı seçtiğinde tüm uygulama anında değişir.
 */
@Component({
  selector: 'SearchConsoleApp-theme-switcher',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="theme-switcher">
      <label>Tema:</label>
      <select [value]="currentName()" (change)="onChange($event)">
        @for (t of options(); track t.name) {
          <option [value]="t.name">{{ t.displayName }} ({{ modeLabel(t.mode) }})</option>
        }
      </select>
    </div>
  `,
  styles: [`
    .theme-switcher {
      display: inline-flex;
      align-items: center;
      gap: var(--space-2);
      color: var(--color-text-muted);
      font-size: 0.875rem;

      select {
        background: var(--color-surface);
        color: var(--color-text);
        border: 1px solid var(--color-border);
        border-radius: var(--radius-sm);
        padding: var(--space-1) var(--space-2);
      }
    }
  `]
})
export class ThemeSwitcherComponent implements OnInit {
  private themeService = inject(ThemeService);
  private loader = inject(ThemeLoaderService);

  options = signal<ThemeOption[]>([]);
  currentName = signal<string>('');

  ngOnInit() {
    // Built-in'leri başlangıçta listeye koy
    const builtIn: ThemeOption[] = BUILT_IN_THEMES.map(t => ({
      name: t.name,
      displayName: t.displayName,
      mode: t.mode
    }));

    // Asset/backend listesini eklemeye çalış
    this.loader.list().subscribe(extras => {
      const merged = [...builtIn];
      for (const e of extras) {
        if (!merged.find(m => m.name === e.name)) merged.push(e);
      }
      this.options.set(merged);
    });

    this.currentName.set(this.themeService.current()?.name ?? '');
  }

  modeLabel(mode: string): string {
    return mode === 'dark' ? 'koyu' : 'açık';
  }

  onChange(event: Event) {
    const name = (event.target as HTMLSelectElement).value;

    // Built-in mi?
    const builtIn = BUILT_IN_THEMES.find(t => t.name === name);
    if (builtIn) {
      this.themeService.apply(builtIn);
      this.currentName.set(name);
      return;
    }

    // Network'ten
    this.loader.load(name).subscribe(theme => {
      this.themeService.apply(theme);
      this.currentName.set(name);
    });
  }
}
