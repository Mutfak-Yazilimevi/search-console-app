import {
  Component,
  ChangeDetectionStrategy,
  inject,
  input,
  signal,
  DestroyRef,
  computed,
} from '@angular/core';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { AuditApiService } from './audit-api.service';
import {
  AltTextGenerationDto,
  FaqGenerationDto,
  MetaGenerationDto,
} from './audit.models';
import { parseImgAltMissingEvidence } from './audit-img-alt-evidence';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';

export type AuditAiAction = 'faq' | 'meta' | 'alt-text';
export type MetaAiTarget = 'seo' | 'openGraph';

@Component({
  selector: 'SearchConsoleApp-audit-ai-actions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonComponent],
  template: `
    <div class="audit-ai">
      <SearchConsoleApp-button
        variant="secondary"
        size="sm"
        [disabled]="loading() || !geminiEnabled()"
        (click)="onGenerate()">
        {{ loading() ? 'Oluşturuluyor…' : buttonLabel() }}
      </SearchConsoleApp-button>

      @if (!geminiEnabled()) {
        <p class="audit-ai-hint">Gemini devre dışı veya yapılandırılmamış. Sistem entegrasyonları panelinden etkinleştirin.</p>
      }

      @if (error()) {
        <p class="audit-ai-error">{{ error() }}</p>
      }

      @if (setupHint()) {
        <p class="audit-ai-hint">{{ setupHint() }}</p>
        @if (configKey()) {
          <code class="audit-ai-key">{{ configKey() }}</code>
        }
      }

      @if (faqResult()) {
        <div class="audit-ai-result">
          <p class="audit-ai-note">
            Yapay zekâ taslağı — yayınlamadan önce doğruluk ve uygunluğu kontrol edin.
          </p>
          <ul class="audit-ai-list">
            @for (q of faqResult()!.questions; track q.question) {
              <li><strong>{{ q.question }}</strong><br />{{ q.answer }}</li>
            }
          </ul>
          <div class="audit-ai-actions-row">
            <button type="button" class="link-btn" (click)="copy(faqResult()!.htmlSection)">HTML kopyala</button>
            <button type="button" class="link-btn" (click)="copy(faqResult()!.jsonLd)">JSON-LD kopyala</button>
          </div>
          <details class="audit-ai-code">
            <summary>HTML + JSON-LD önizleme</summary>
            <pre class="guide-example"><code>{{ faqResult()!.htmlSection }}

{{ faqResult()!.jsonLd }}</code></pre>
          </details>
        </div>
      }

      @if (metaResult()) {
        <div class="audit-ai-result">
          <p class="audit-ai-note">
            {{ metaTarget() === 'openGraph' ? 'Önerilen Open Graph etiketleri' : 'Önerilen title ve meta description' }} — sayfa içeriğine göre uyarlayın.
          </p>
          <dl class="meta-dl">
            <dt>Title ({{ metaResult()!.title.length }} karakter)</dt>
            <dd>{{ metaResult()!.title }}</dd>
            <dt>Meta description ({{ metaResult()!.metaDescription.length }} karakter)</dt>
            <dd>{{ metaResult()!.metaDescription }}</dd>
          </dl>
          <div class="audit-ai-actions-row">
            <button type="button" class="link-btn" (click)="copy(metaResult()!.titleTagHtml)">Title etiketi</button>
            <button type="button" class="link-btn" (click)="copy(metaResult()!.metaTagHtml)">Meta etiketi</button>
            <button type="button" class="link-btn" (click)="copy(metaSnippet())">Her ikisi</button>
          </div>
          <details class="audit-ai-code">
            <summary>HTML önizleme</summary>
            <pre class="guide-example"><code>{{ metaSnippet() }}</code></pre>
          </details>
        </div>
      }

      @if (altResult()) {
        <div class="audit-ai-result">
          <p class="audit-ai-note">Önerilen alt metinler — görselleri kontrol edip yayınlayın.</p>
          <table class="alt-table">
            <thead><tr><th>Görsel</th><th>Önerilen alt</th><th></th></tr></thead>
            <tbody>
              @for (img of altResult()!.images; track img.src) {
                <tr>
                  <td class="src">{{ shortSrc(img.src) }}</td>
                  <td>{{ img.altText }}</td>
                  <td><button type="button" class="link-btn" (click)="copy(img.imgHtmlSnippet)">Kopyala</button></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      @if (copied()) {
        <p class="audit-ai-copied">Panoya kopyalandı.</p>
      }
    </div>
  `,
  styles: [`
    .audit-ai { margin-top: 0.5rem; }
    .audit-ai-error { color: #c0392b; font-size: 0.8rem; margin: 0.35rem 0 0; }
    .audit-ai-hint { color: #92400e; font-size: 0.78rem; margin: 0.35rem 0 0; }
    .audit-ai-key { display: block; margin-top: 0.25rem; font-size: 0.72rem; background: #fee2e2; padding: 0.15rem 0.35rem; border-radius: 3px; }
    .audit-ai-result {
      margin-top: 0.75rem; padding: 0.75rem;
      background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px;
    }
    .audit-ai-note { font-size: 0.8rem; color: #64748b; margin: 0 0 0.5rem; }
    .audit-ai-list { margin: 0 0 0.75rem; padding-left: 1.1rem; font-size: 0.85rem; }
    .audit-ai-list li { margin-bottom: 0.5rem; }
    .audit-ai-actions-row { display: flex; gap: 0.75rem; flex-wrap: wrap; margin-bottom: 0.5rem; }
    .link-btn {
      background: none; border: none; color: #4285f4; cursor: pointer;
      text-decoration: underline; font-size: 0.85rem; padding: 0;
    }
    .audit-ai-code summary { cursor: pointer; font-size: 0.85rem; color: #555; }
    .guide-example {
      margin: 0.5rem 0 0; padding: 0.75rem; overflow-x: auto;
      background: #1e293b; color: #e2e8f0; border-radius: 6px;
      font-size: 0.72rem; line-height: 1.45;
    }
    .guide-example code { font-family: ui-monospace, Menlo, monospace; white-space: pre-wrap; word-break: break-word; }
    .audit-ai-copied { font-size: 0.75rem; color: #27ae60; margin: 0.25rem 0 0; }
    .meta-dl { margin: 0 0 0.5rem; font-size: 0.85rem; }
    .meta-dl dt { font-weight: 600; color: #475569; margin-top: 0.35rem; }
    .meta-dl dd { margin: 0.15rem 0 0; color: #1e293b; }
    .alt-table { width: 100%; border-collapse: collapse; font-size: 0.78rem; margin-top: 0.35rem; }
    .alt-table th, .alt-table td { padding: 0.35rem 0.4rem; border-bottom: 1px solid #e2e8f0; text-align: left; vertical-align: top; }
    .alt-table th { background: #f1f5f9; }
    .alt-table .src { max-width: 140px; word-break: break-all; color: #64748b; }
  `],
})
export class AuditAiActionsComponent {
  private auditApi = inject(AuditApiService);
  private destroyRef = inject(DestroyRef);

  auditEntityId = input.required<string>();
  pageUrl = input.required<string>();
  action = input.required<AuditAiAction>();
  metaTarget = input<MetaAiTarget>('seo');
  evidence = input<string | null | undefined>(null);

  geminiEnabled = input(true);

  loading = signal(false);
  error = signal<string | null>(null);
  setupHint = signal<string | null>(null);
  configKey = signal<string | null>(null);
  faqResult = signal<FaqGenerationDto | null>(null);
  metaResult = signal<MetaGenerationDto | null>(null);
  altResult = signal<AltTextGenerationDto | null>(null);
  copied = signal(false);

  buttonLabel = computed(() => {
    switch (this.action()) {
      case 'meta':
        return this.metaTarget() === 'openGraph' ? 'AI ile OG öner' : 'AI ile meta öner';
      case 'alt-text': return 'AI ile alt metin öner';
      default: return 'AI ile oluştur';
    }
  });

  metaSnippet = computed(() => {
    const m = this.metaResult();
    if (!m) return '';
    return `${m.titleTagHtml}\n${m.metaTagHtml}`;
  });

  onGenerate(): void {
    const url = this.pageUrl()?.trim();
    const auditId = this.auditEntityId();
    if (!url || !auditId) return;

    this.loading.set(true);
    this.error.set(null);
    this.setupHint.set(null);
    this.configKey.set(null);
    this.faqResult.set(null);
    this.metaResult.set(null);
    this.altResult.set(null);
    this.copied.set(false);

    const handleError = (err: { error?: unknown }) => {
      const body = err?.error as Record<string, string> | string | undefined;
      const msg = typeof body === 'string'
        ? body
        : body?.['message'] ?? body?.['title'] ?? body?.['detail'];
      this.error.set(msg ?? 'AI içerik oluşturulamadı.');
      if (typeof body === 'object' && body && (body['code'] === 'gemini_config_missing' || body['setupHint'])) {
        this.setupHint.set(body['setupHint'] ?? null);
        this.configKey.set(body['configKey'] ?? null);
      }
      this.loading.set(false);
      return of(null);
    };

    const kind = this.action();
    if (kind === 'meta') {
      this.auditApi.generateMeta(auditId, url, this.metaTarget()).pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(handleError),
      ).subscribe((data) => {
        this.loading.set(false);
        if (data) this.metaResult.set(data);
      });
      return;
    }

    if (kind === 'alt-text') {
      const parsed = parseImgAltMissingEvidence(this.evidence());
      const imageSrcs = parsed?.images.map((i) => i.src);
      this.auditApi.generateAltText(auditId, url, imageSrcs).pipe(
        takeUntilDestroyed(this.destroyRef),
        catchError(handleError),
      ).subscribe((data) => {
        this.loading.set(false);
        if (data) this.altResult.set(data);
      });
      return;
    }

    this.auditApi.generateFaq(auditId, url).pipe(
      takeUntilDestroyed(this.destroyRef),
      catchError(handleError),
    ).subscribe((data) => {
      this.loading.set(false);
      if (data) this.faqResult.set(data);
    });
  }

  shortSrc(src: string): string {
    if (src.length <= 48) return src;
    return src.slice(0, 45) + '…';
  }

  copy(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
  }
}
