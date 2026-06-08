import { Component, input, output, ChangeDetectionStrategy } from '@angular/core';

/**
 * Ortak buton. CSS custom property'leri kullanır → tema otomatik etki eder.
 * Hiçbir uygulamada yeniden yazılmaz; sadece bunu import et.
 */
@Component({
  selector: 'SearchConsoleApp-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [type]="type()"
      [disabled]="disabled()"
      [class]="'btn btn-' + variant() + ' btn-' + size()"
      (click)="clicked.emit()">
      <ng-content></ng-content>
    </button>
  `,
  styles: [`
    .btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: var(--space-2);
      font-weight: 500;
      border-radius: var(--radius-md);
      border: 1px solid transparent;
      cursor: pointer;
      transition: background 120ms ease, border-color 120ms ease, transform 80ms ease;
      &:disabled { opacity: 0.5; cursor: not-allowed; }
      &:active:not(:disabled) { transform: translateY(1px); }
    }

    .btn-sm { padding: var(--space-1) var(--space-3); font-size: 0.875rem; }
    .btn-md { padding: var(--space-2) var(--space-4); font-size: 1rem; }
    .btn-lg { padding: var(--space-3) var(--space-6); font-size: 1.125rem; }

    .btn-primary {
      background: var(--color-primary);
      color: var(--color-primary-foreground);
      &:hover:not(:disabled) { background: var(--color-primary-hover); }
      &:active:not(:disabled) { background: var(--color-primary-active); }
    }

    .btn-secondary {
      background: var(--color-surface);
      color: var(--color-text);
      border-color: var(--color-border);
      &:hover:not(:disabled) { background: var(--color-surface-elevated); border-color: var(--color-border-strong); }
    }

    .btn-danger {
      background: var(--color-danger);
      color: white;
      &:hover:not(:disabled) { filter: brightness(1.1); }
    }

    .btn-ghost {
      background: transparent;
      color: var(--color-text);
      &:hover:not(:disabled) { background: var(--color-surface); }
    }
  `]
})
export class ButtonComponent {
  variant = input<'primary' | 'secondary' | 'danger' | 'ghost'>('primary');
  size = input<'sm' | 'md' | 'lg'>('md');
  type = input<'button' | 'submit' | 'reset'>('button');
  disabled = input<boolean>(false);

  clicked = output<void>();
}
