import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MerchantCenterApiService } from './merchant-center-api.service';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'SearchConsoleApp-gmc-callback',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <section class="surface" style="padding: 2rem; text-align: center;">
      @if (error()) {
        <p style="color: #c0392b;">{{ error() }}</p>
        <a routerLink="/merchant-center">Ürün uyumluluğuna dön</a>
      } @else {
        <p>Merchant Center bağlantısı tamamlanıyor…</p>
      }
    </section>
  `,
})
export class MerchantCenterCallbackComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private gmcApi = inject(MerchantCenterApiService);

  error = signal<string | null>(null);

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    const state = this.route.snapshot.queryParamMap.get('state');
    if (!code || !state) {
      this.error.set('OAuth callback parametreleri eksik.');
      return;
    }

    this.gmcApi.completeCallback(code, state).pipe(
      catchError(() => {
        this.error.set('Merchant Center bağlantısı başarısız.');
        return of(null);
      }),
    ).subscribe((res) => {
      if (res?.ok) this.router.navigateByUrl('/merchant-center');
    });
  }
}
