import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';
import { ThemeSwitcherComponent, ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { AuthService } from '@SearchConsoleApp/shared/core';

@Component({
  selector: 'SearchConsoleApp-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, ThemeSwitcherComponent, ButtonComponent],
  template: `
    <div class="layout">
      <aside>
        <h1>Admin</h1>
        <nav>
          <a routerLink="/customers">Müşteriler</a>
        </nav>
      </aside>
      <div class="main">
        <header>
          @if (auth.isAuthenticated()) {
            <span>{{ auth.user()?.email }}</span>
            <SearchConsoleApp-button variant="ghost" size="sm" (clicked)="auth.logout()">Çıkış</SearchConsoleApp-button>
          }
          <SearchConsoleApp-theme-switcher></SearchConsoleApp-theme-switcher>
        </header>
        <main><router-outlet></router-outlet></main>
      </div>
    </div>
  `,
  styles: [`
    .layout { display: grid; grid-template-columns: 220px 1fr; min-height: 100vh; }
    aside { background: var(--color-surface); border-right: 1px solid var(--color-border); padding: var(--space-4); }
    aside h1 { margin: 0 0 var(--space-6) 0; color: var(--color-primary); font-size: 1.125rem; }
    nav { display: flex; flex-direction: column; gap: var(--space-2); }
    nav a { padding: var(--space-2) var(--space-3); border-radius: var(--radius-sm); color: var(--color-text); }
    nav a:hover { background: var(--color-surface-elevated); }
    .main { display: flex; flex-direction: column; }
    header { display: flex; justify-content: flex-end; gap: var(--space-3); align-items: center; padding: var(--space-4) var(--space-6); border-bottom: 1px solid var(--color-border); }
    main { padding: var(--space-6); flex: 1; }
  `]
})
export class AppComponent {
  auth = inject(AuthService);
}
