import { Component, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeSwitcherComponent } from '@SearchConsoleApp/shared/ui';
import { NotificationToastComponent } from '@SearchConsoleApp/shared/core';

@Component({
  selector: 'SearchConsoleApp-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, ThemeSwitcherComponent, NotificationToastComponent],
  template: `
    <header class="header">
      <h1>SEO Site Denetimi</h1>
      <SearchConsoleApp-theme-switcher></SearchConsoleApp-theme-switcher>
    </header>
    <SearchConsoleApp-notification-toast />
    <main><router-outlet></router-outlet></main>
  `,
  styles: [`
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: var(--space-4) var(--space-6);
      background: var(--color-surface);
      border-bottom: 1px solid var(--color-border);
    }
    h1 { margin: 0; font-size: 1.25rem; color: var(--color-primary); }
    main { padding: var(--space-6); }
  `]
})
export class AppComponent {}
