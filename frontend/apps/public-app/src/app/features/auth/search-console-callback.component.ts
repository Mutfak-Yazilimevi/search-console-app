import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SearchConsoleApiService } from '../audit/search-console-api.service';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'SearchConsoleApp-sc-callback',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <section class="surface" style="padding: 2rem; text-align: center;">
      @if (error()) {
        <p style="color: #c0392b;">{{ error() }}</p>
        <a routerLink="/">Ana sayfaya dön</a>
      } @else {
        <p>Search Console bağlantısı tamamlanıyor…</p>
      }
    </section>
  `,
})
export class SearchConsoleCallbackComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private scApi = inject(SearchConsoleApiService);

  error = signal<string | null>(null);

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    const state = this.route.snapshot.queryParamMap.get('state');
    if (!code || !state) {
      this.error.set('OAuth callback parametreleri eksik.');
      return;
    }

    this.scApi.completeCallback(code, state).pipe(
      catchError(() => {
        this.error.set('Search Console bağlantısı başarısız.');
        return of(null);
      }),
    ).subscribe((res) => {
      if (res?.ok) this.router.navigateByUrl('/');
    });
  }
}
