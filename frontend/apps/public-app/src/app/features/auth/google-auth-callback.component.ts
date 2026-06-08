import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '@SearchConsoleApp/shared/core';
import { ApiResponse, AuthTokens } from '@SearchConsoleApp/shared/models';
import { catchError, map, of } from 'rxjs';
import { APP_CONFIG } from '@SearchConsoleApp/shared/core';

@Component({
  selector: 'SearchConsoleApp-google-callback',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <section class="surface" style="padding: 2rem; text-align: center;">
      @if (error()) {
        <p style="color: #c0392b;">{{ error() }}</p>
        <a routerLink="/">Ana sayfaya dön</a>
      } @else {
        <p>Google girişi tamamlanıyor…</p>
      }
    </section>
  `,
})
export class GoogleAuthCallbackComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private config = inject(APP_CONFIG);

  error = signal<string | null>(null);

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    const state = this.route.snapshot.queryParamMap.get('state');
    if (!code || !state) {
      this.error.set('OAuth callback parametreleri eksik.');
      return;
    }

    const base = this.config.apiRootUrl.replace(/\/$/, '');
    this.http.post<ApiResponse<AuthTokens>>(
      `${base}/public/auth/external/google/callback`,
      { code, state },
    ).pipe(
      map((r) => r.data),
      catchError(() => {
        this.error.set('Google girişi başarısız.');
        return of(null);
      }),
    ).subscribe((tokens) => {
      if (!tokens) return;
      localStorage.setItem(this.config.tokenStorageKey, tokens.accessToken);
      localStorage.setItem(this.config.tokenStorageKey + '.refresh', tokens.refreshToken);
      localStorage.setItem(this.config.tokenStorageKey + '.user', JSON.stringify(tokens.user));
      this.router.navigateByUrl('/');
    });
  }
}
