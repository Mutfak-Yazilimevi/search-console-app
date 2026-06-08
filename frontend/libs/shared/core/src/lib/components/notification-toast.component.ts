import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NotificationService } from '../notification.service';

interface ToastItem {
  id: string;
  title: string;
  message: string;
  severity: string;
}

@Component({
  selector: 'SearchConsoleApp-notification-toast',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toast-stack" aria-live="polite">
      @for (toast of toasts(); track toast.id) {
        <article class="toast" [class]="toastClass(toast.severity)">
          <div class="toast-body">
            <strong>{{ toast.title }}</strong>
            <p>{{ toast.message }}</p>
          </div>
          <button type="button" class="toast-close" (click)="dismiss(toast.id)" aria-label="Kapat">×</button>
        </article>
      }
    </div>
  `,
  styles: [`
    .toast-stack {
      position: fixed;
      top: 1rem;
      right: 1rem;
      z-index: 10000;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      max-width: min(24rem, calc(100vw - 2rem));
    }
    .toast {
      display: flex;
      gap: 0.5rem;
      align-items: flex-start;
      padding: 0.75rem 0.85rem;
      border-radius: 8px;
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.12);
      border: 1px solid transparent;
      background: #fff;
      color: #222;
    }
    .toast-info { border-color: #bee5eb; background: #e8f7fb; }
    .toast-warning { border-color: #ffeeba; background: #fff8e6; color: #856404; }
    .toast-error { border-color: #f5c6cb; background: #fdecea; color: #721c24; }
    .toast-body { flex: 1; min-width: 0; }
    .toast-body strong { display: block; font-size: 0.9rem; margin-bottom: 0.2rem; }
    .toast-body p { margin: 0; font-size: 0.82rem; line-height: 1.35; }
    .toast-close {
      border: none;
      background: transparent;
      font-size: 1.2rem;
      line-height: 1;
      cursor: pointer;
      opacity: 0.65;
      color: inherit;
      padding: 0;
    }
    .toast-close:hover { opacity: 1; }
  `],
})
export class NotificationToastComponent implements OnInit {
  private notifications = inject(NotificationService);
  private destroyRef = inject(DestroyRef);

  readonly toasts = signal<ToastItem[]>([]);

  ngOnInit(): void {
    this.notifications.userNotification$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((e) => {
      this.push(e.title, e.message, e.severity ?? 'info');
    });
  }

  toastClass(severity: string): string {
    if (severity === 'error') return 'toast toast-error';
    if (severity === 'warning') return 'toast toast-warning';
    return 'toast toast-info';
  }

  dismiss(id: string): void {
    this.toasts.update((items) => items.filter((t) => t.id !== id));
  }

  private push(title: string, message: string, severity: string): void {
    const id = crypto.randomUUID();
    this.toasts.update((items) => [...items, { id, title, message, severity }].slice(-4));
    setTimeout(() => this.dismiss(id), 12_000);
  }
}
