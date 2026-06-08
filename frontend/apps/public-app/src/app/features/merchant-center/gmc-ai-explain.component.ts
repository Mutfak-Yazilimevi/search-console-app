import { Component, ChangeDetectionStrategy, inject, input, signal, DestroyRef } from '@angular/core';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { ProductComplianceApiService } from './product-compliance-api.service';
import { GmcAiExplainDto } from './merchant-center.models';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'SearchConsoleApp-gmc-ai-explain',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonComponent],
  template: `
    @if (enabled()) {
      <div class="gmc-ai-explain">
        <SearchConsoleApp-button variant="secondary" size="sm" [disabled]="loading()" (click)="onExplain()">
          {{ loading() ? 'Açıklanıyor…' : 'Detaylı AI önerisi' }}
        </SearchConsoleApp-button>
      @if (error()) {
        <p class="gmc-ai-error">{{ error() }}</p>
      }
      @if (result()) {
        <div class="gmc-ai-result">
          <p class="gmc-ai-note">Yapay zekâ taslağı — yayınlamadan önce doğrulayın.</p>
          <p>{{ result()!.explanation }}</p>
          @if (result()!.steps.length) {
            <ol>
              @for (step of result()!.steps; track step) {
                <li>{{ step }}</li>
              }
            </ol>
          }
        </div>
      }
    </div>
    }
  `,
  styles: [`
    .gmc-ai-explain { margin-top: 0.35rem; }
    .gmc-ai-error { color: #c0392b; font-size: 0.8rem; }
    .gmc-ai-result { margin-top: 0.5rem; padding: 0.75rem; background: #f8fafc; border-radius: 8px; border: 1px solid #e2e8f0; font-size: 0.85rem; }
    .gmc-ai-note { font-size: 0.75rem; color: #64748b; margin: 0 0 0.5rem; }
    ol { margin: 0.5rem 0 0; padding-left: 1.2rem; }
  `],
})
export class GmcAiExplainComponent {
  runEntityId = input.required<string>();
  issueEntityId = input.required<string>();
  enabled = input(true);

  private api = inject(ProductComplianceApiService);
  private destroyRef = inject(DestroyRef);

  loading = signal(false);
  error = signal<string | null>(null);
  result = signal<GmcAiExplainDto | null>(null);

  onExplain(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.aiExplainIssue(this.runEntityId(), this.issueEntityId()).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((e) => {
        this.error.set(e?.error?.message ?? 'AI açıklaması alınamadı.');
        return of(null);
      }),
    ).subscribe((r) => {
      this.loading.set(false);
      if (r) this.result.set(r);
    });
  }
}
