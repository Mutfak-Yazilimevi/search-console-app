import { Component, ChangeDetectionStrategy, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { AuthService } from '@SearchConsoleApp/shared/core';

@Component({
  selector: 'SearchConsoleApp-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, ButtonComponent],
  template: `
    <section class="login surface-elevated">
      <h2>Giriş</h2>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>E-posta
          <input type="email" formControlName="email" />
        </label>
        <label>Şifre
          <input type="password" formControlName="password" />
        </label>
        @if (error()) {
          <div class="error">{{ error() }}</div>
        }
        <SearchConsoleApp-button type="submit" [disabled]="form.invalid || loading()">Giriş</SearchConsoleApp-button>
      </form>
    </section>
  `,
  styles: [`
    .login { max-width: 360px; margin: 4rem auto; padding: var(--space-6); }
    form { display: flex; flex-direction: column; gap: var(--space-3); }
    label { display: flex; flex-direction: column; gap: var(--space-1); color: var(--color-text-muted); font-size: 0.875rem; }
    input { padding: var(--space-2) var(--space-3); border: 1px solid var(--color-border); border-radius: var(--radius-sm); background: var(--color-background); color: var(--color-text); }
    .error { color: var(--color-danger); font-size: 0.875rem; }
    h2 { margin: 0 0 var(--space-4) 0; color: var(--color-text); }
  `]
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  loading = signal(false);
  error = signal<string | null>(null);

  submit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set(null);
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => { this.loading.set(false); this.router.navigateByUrl('/'); },
      error: (err) => { this.loading.set(false); this.error.set(err?.error?.detail ?? 'Giriş başarısız'); }
    });
  }
}
