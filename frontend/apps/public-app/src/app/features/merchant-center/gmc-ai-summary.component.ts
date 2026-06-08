import { Component, ChangeDetectionStrategy, inject, input, signal, DestroyRef } from '@angular/core';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { ProductComplianceApiService } from './product-compliance-api.service';
import { GmcAiSummaryDto } from './merchant-center.models';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'SearchConsoleApp-gmc-ai-summary',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonComponent],
  template: `
    <div class="gmc-ai">
      @if (enabled()) {
        <SearchConsoleApp-button variant="secondary" size="sm" [disabled]="loading()" (click)="onGenerate()">
          {{ loading() ? 'Oluşturuluyor…' : 'AI önerileri' }}
        </SearchConsoleApp-button>
      } @else {
        <p class="gmc-ai-disabled">Gemini yapılandırılmamış — statik öneriler kullanılabilir.</p>
      }
      @if (error()) {
        <p class="gmc-ai-error">{{ error() }}</p>
      }
      @if (result()) {
        <div class="gmc-ai-result">
          <p class="gmc-ai-note">Yapay zekâ taslağı — yayınlamadan önce doğrulayın.</p>
          <div class="gmc-ai-md">{{ result()!.summaryMarkdown }}</div>
          @if (result()!.priorities.length) {
            <ul>
              @for (p of result()!.priorities; track p) {
                <li>{{ p }}</li>
              }
            </ul>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .gmc-ai-error { color: #c0392b; font-size: 0.8rem; margin: 0.35rem 0 0; }
    .gmc-ai-disabled { font-size: 0.8rem; color: #64748b; margin: 0; }
    .gmc-ai-result { margin-top: 0.75rem; padding: 0.75rem; background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; }
    .gmc-ai-note { font-size: 0.8rem; color: #64748b; margin: 0 0 0.5rem; }
    .gmc-ai-md { white-space: pre-wrap; font-size: 0.85rem; margin-bottom: 0.5rem; }
    ul { margin: 0; padding-left: 1.1rem; font-size: 0.85rem; }
  `],
})
export class GmcAiSummaryComponent {
  runEntityId = input.required<string>();
  enabled = input(true);
  private api = inject(ProductComplianceApiService);
  private destroyRef = inject(DestroyRef);

  loading = signal(false);
  error = signal<string | null>(null);
  result = signal<GmcAiSummaryDto | null>(null);

  onGenerate(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.aiSummary(this.runEntityId()).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((e) => {
        this.error.set(e?.error?.message ?? 'AI önerisi alınamadı.');
        return of(null);
      }),
    ).subscribe((r) => {
      this.loading.set(false);
      if (r) this.result.set(r);
    });
  }
}
