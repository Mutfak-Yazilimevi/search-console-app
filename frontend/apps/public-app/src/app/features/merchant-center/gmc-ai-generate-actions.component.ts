import { Component, ChangeDetectionStrategy, inject, input, signal, DestroyRef, computed } from '@angular/core';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { ProductComplianceApiService } from './product-compliance-api.service';
import { GmcAiGenerateDto } from './merchant-center.models';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'SearchConsoleApp-gmc-ai-generate',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonComponent],
  template: `
    @if (generateType() && enabled()) {
      <div class="gmc-ai">
        <SearchConsoleApp-button variant="secondary" size="sm" [disabled]="loading()" (click)="onGenerate()">
          {{ loading() ? 'Oluşturuluyor…' : 'AI ile oluştur' }}
        </SearchConsoleApp-button>
        @if (error()) {
          <p class="gmc-ai-error">{{ error() }}</p>
        }
        @if (result()) {
          <div class="gmc-ai-result">
            <p class="gmc-ai-note">Yapay zekâ taslağı — yayınlamadan önce doğrulayın.</p>
            <pre class="gmc-ai-code">{{ result()!.content }}</pre>
            <button type="button" class="link-btn" (click)="copy()">Kopyala</button>
            @if (copied()) { <span class="copied">Panoya kopyalandı.</span> }
          </div>
        }
      </div>
    }
  `,
  styles: [`
    .gmc-ai-error { color: #c0392b; font-size: 0.8rem; }
    .gmc-ai-result { margin-top: 0.5rem; padding: 0.75rem; background: #f8fafc; border-radius: 8px; border: 1px solid #e2e8f0; }
    .gmc-ai-note { font-size: 0.75rem; color: #64748b; }
    .gmc-ai-code { font-size: 0.72rem; overflow-x: auto; white-space: pre-wrap; margin: 0.5rem 0; }
    .link-btn { background: none; border: none; color: #4285f4; cursor: pointer; text-decoration: underline; }
    .copied { font-size: 0.75rem; color: #16a34a; margin-left: 0.5rem; }
  `],
})
export class GmcAiGenerateActionsComponent {
  runEntityId = input.required<string>();
  productEntityId = input<string | null>(null);
  ruleId = input.required<string>();
  enabled = input(true);

  private api = inject(ProductComplianceApiService);
  private destroyRef = inject(DestroyRef);

  loading = signal(false);
  error = signal<string | null>(null);
  result = signal<GmcAiGenerateDto | null>(null);
  copied = signal(false);

  generateType = computed(() => {
    const id = this.ruleId();
    if (['GMC-SPEC-002'].includes(id)) return 'title';
    if (['GMC-SPEC-003', 'GMC-X-004'].includes(id)) return 'description';
    if (['GMC-SPEC-006', 'GMC-SPEC-007', 'GMC-SPEC-011'].includes(id)) return 'schema';
    if (['GMC-SITE-001'].includes(id)) return 'return-policy';
    if (['GMC-SITE-002'].includes(id)) return 'shipping';
    if (['GMC-SITE-005', 'GMC-SITE-006', 'GMC-SITE-007', 'GMC-SITE-008'].includes(id)) return 'shipping';
    return null;
  });

  onGenerate(): void {
    const type = this.generateType();
    if (!type) return;
    this.loading.set(true);
    this.error.set(null);

    const productId = this.productEntityId();
    const req = productId
      ? this.api.aiGenerateProduct(this.runEntityId(), productId, type)
      : this.api.aiGenerateSite(this.runEntityId(), type);

    req.pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError((e) => {
        this.error.set(e?.error?.message ?? 'AI içerik üretilemedi.');
        return of(null);
      }),
    ).subscribe((r) => {
      this.loading.set(false);
      if (r) this.result.set(r);
    });
  }

  copy(): void {
    const text = this.result()?.content;
    if (!text) return;
    navigator.clipboard.writeText(text).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
  }
}
