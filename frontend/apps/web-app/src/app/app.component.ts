import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeSwitcherComponent, ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { AuthService } from '@SearchConsoleApp/shared/core';

@Component({
  selector: 'SearchConsoleApp-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, ThemeSwitcherComponent, ButtonComponent],
  template: `
    <header class="header">
      <h1>SearchConsoleApp Web</h1>
      <div class="header-right">
        @if (auth.isAuthenticated()) {
          <span class="user">{{ auth.user()?.email }}</span>
          <SearchConsoleApp-button variant="ghost" size="sm" (clicked)="auth.logout()">Çıkış</SearchConsoleApp-button>
        }
        <SearchConsoleApp-theme-switcher></SearchConsoleApp-theme-switcher>
      </div>
    </header>
    <main><router-outlet></router-outlet></main>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; padding: var(--space-4) var(--space-6); background: var(--color-surface); border-bottom: 1px solid var(--color-border); }
    .header-right { display: flex; gap: var(--space-3); align-items: center; }
    .user { color: var(--color-text-muted); font-size: 0.875rem; }
    h1 { margin: 0; font-size: 1.25rem; color: var(--color-primary); }
    main { padding: var(--space-6); }
  `]
})
export class AppComponent {
  auth = inject(AuthService);
}
