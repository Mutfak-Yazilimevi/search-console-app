import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ProductComplianceIssueDto } from './merchant-center.models';
import { GmcAiGenerateActionsComponent } from './gmc-ai-generate-actions.component';
import { GmcAiExplainComponent } from './gmc-ai-explain.component';
import { issueProductEntries, issueProductListLabel, parseIssueEvidence } from './gmc-evidence.utils';

@Component({
  selector: 'SearchConsoleApp-gmc-issue-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [GmcAiGenerateActionsComponent, GmcAiExplainComponent],
  template: `
    @for (issue of issues(); track issue.entityId) {
      <div class="issue-row">
        <span class="badge" [class]="issue.severity.toLowerCase()">{{ issue.severity }}</span>
        @if (showSource()) {
          <span class="source">{{ issue.source }}</span>
        }
        <strong>{{ issue.ruleId }}</strong> — {{ issue.message }}
        @if (issue.pageUrl) {
          <div><a [href]="issue.pageUrl" target="_blank" rel="noopener">{{ issue.pageUrl }}</a></div>
        }
        <p class="fix-hint">{{ issue.fixHint }}</p>
        @if (issue.evidence) {
          @if (parseEvidence(issue); as ev) {
            @if (ev.title) {
              <div class="evidence">
                <span class="evidence-label">Tekrarlanan başlık:</span>
                <div class="evidence-value">{{ ev.title }}</div>
              </div>
            }
            @if (ev.descriptionPreview) {
              <div class="evidence">
                <span class="evidence-label">Tekrarlanan açıklama:</span>
                <div class="evidence-value">{{ ev.descriptionPreview }}</div>
              </div>
            }
            @if (ev.schemaPrice || ev.visiblePrice) {
              <div class="evidence">
                <span class="evidence-label">Fiyat karşılaştırması:</span>
                @if (ev.schemaPrice) {
                  <div><strong>Schema (JSON-LD):</strong> {{ ev.schemaPrice }}</div>
                }
                @if (ev.visiblePrice) {
                  <div><strong>Sayfada görünen:</strong> {{ ev.visiblePrice }}</div>
                }
                @if (ev.schemaListPrice) {
                  <div><strong>Schema liste:</strong> {{ ev.schemaListPrice }}</div>
                }
              </div>
            }
            @if (productEntries(ev).length) {
              <div class="evidence">
                <span class="evidence-label">
                  {{ productListLabel(issue.ruleId) }} ({{ productEntries(ev).length }}):
                </span>
                @for (p of productEntries(ev); track p.url; let i = $index) {
                  <div class="product-entry">
                    <span class="product-index">{{ i + 1 }}.</span>
                    @if (p.title && (!ev.descriptionPreview || p.title !== ev.title)) {
                      <div class="product-title">{{ p.title }}</div>
                    }
                    <a [href]="p.url" target="_blank" rel="noopener">{{ p.url }}</a>
                    @if (p.meta) {
                      <div class="product-meta">{{ p.meta }}</div>
                    }
                  </div>
                }
              </div>
            }
          }
        }
        @if (issue.docUrl) {
          <a [href]="issue.docUrl" target="_blank" rel="noopener">Dokümantasyon</a>
        }
        @if (showAi()) {
          <SearchConsoleApp-gmc-ai-generate
            [runEntityId]="runEntityId()"
            [productEntityId]="productEntityId()"
            [ruleId]="issue.ruleId"
            [enabled]="aiEnabled()" />
          <SearchConsoleApp-gmc-ai-explain
            [runEntityId]="runEntityId()"
            [issueEntityId]="issue.entityId"
            [enabled]="aiEnabled()" />
        }
      </div>
    }
  `,
  styles: [`
    .issue-row { border-top: 1px solid #f1f5f9; padding: 0.75rem 0; }
    .badge { font-size: 0.7rem; padding: 0.1rem 0.4rem; border-radius: 4px; margin-right: 0.35rem; }
    .badge.critical { background: #fee2e2; color: #991b1b; }
    .badge.warning { background: #fef3c7; color: #92400e; }
    .badge.info { background: #e0f2fe; color: #075985; }
    .fix-hint { font-size: 0.85rem; color: #475569; margin: 0.25rem 0; }
    .evidence { font-size: 0.8rem; margin: 0.35rem 0 0.5rem; color: #334155; }
    .evidence-label { display: block; font-weight: 600; margin-bottom: 0.25rem; }
    .evidence-value { margin-bottom: 0.35rem; }
    .evidence a { word-break: break-all; font-size: 0.75rem; }
    .product-entry { margin-bottom: 0.35rem; display: flex; flex-direction: column; gap: 0.1rem; }
    .product-index { font-size: 0.75rem; font-weight: 600; color: #64748b; }
    .product-title { font-weight: 600; font-size: 0.8rem; margin-bottom: 0.1rem; }
    .product-meta { font-size: 0.75rem; color: #64748b; margin-top: 0.1rem; }
    .source { font-size: 0.7rem; color: #64748b; margin-right: 0.25rem; }
  `],
})
export class GmcIssueListComponent {
  issues = input<ProductComplianceIssueDto[]>([]);
  runEntityId = input.required<string>();
  productEntityId = input<string | undefined>();
  aiEnabled = input(true);
  showAi = input(true);
  showSource = input(false);

  parseEvidence(issue: ProductComplianceIssueDto) {
    return parseIssueEvidence(issue.ruleId, issue.evidence);
  }

  productEntries(ev: ReturnType<typeof parseIssueEvidence>) {
    return issueProductEntries(ev);
  }

  productListLabel = issueProductListLabel;
}
