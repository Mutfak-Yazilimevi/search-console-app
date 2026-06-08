import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';
import { ButtonComponent, HintIconComponent } from '@SearchConsoleApp/shared/ui';
import type { AuditIssueDto } from './audit.models';
import { categoryLabel } from './audit-labels';
import {
  extractNewCriticalRuleIds,
  groupCriticalIssues,
  type IssueGroup,
} from './audit-issue.utils';
import { getRulePlaybookOrFallback, type RulePlaybookEntry } from './rule-playbook';
import { AUDIT_HINTS } from './audit-metric-hints';

@Component({
  selector: 'SearchConsoleApp-critical-issues-report',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonComponent, HintIconComponent],
  template: `
    <section class="critical-report" [class.has-critical]="criticalCount() > 0">
      <header class="critical-header">
        <div>
          <h3>
            Kritik Sorunlar Raporu
            <SearchConsoleApp-hint [text]="hints.criticalReport" />
          </h3>
          @if (criticalCount() > 0) {
            <p class="critical-summary">
              <strong>{{ criticalCount() }}</strong> kritik kayıt ·
              <strong>{{ distinctRuleCount() }}</strong> farklı kural ·
              <strong>{{ affectedPageCount() }}</strong> etkilenen sayfa
              @if (newRuleIds().size > 0) {
                · <span class="new-badge">{{ newRuleIds().size }} yeni (önceki taramaya göre)</span>
              }
            </p>
          } @else {
            <p class="critical-ok">Bu taramada kritik düzeyinde sorun tespit edilmedi.</p>
          }
        </div>
        @if (canExport()) {
          <SearchConsoleApp-button variant="secondary" size="sm" (click)="exportClicked.emit()">
            Kritik raporu (HTML/PDF)
          </SearchConsoleApp-button>
        }
      </header>

      @if (criticalCount() > 0) {
        <div class="critical-groups">
          @for (group of criticalGroups(); track group.key) {
            <details class="critical-item" [open]="newRuleIds().has(group.ruleId)">
              <summary>
                @if (newRuleIds().has(group.ruleId)) {
                  <span class="pill new">Yeni</span>
                }
                <span class="pill rule">{{ group.ruleId }}</span>
                <span class="msg">{{ group.label }}</span>
                <span class="meta">{{ group.issues.length }} sayfa · {{ categoryLabel(group.category) }}</span>
              </summary>
              <div class="critical-body">
                @if (group.fixHint) {
                  <p class="fix"><strong>Öneri:</strong> {{ group.fixHint }}</p>
                }
                @if (playbookFor(group); as guide) {
                  <ul class="tips">
                    @for (tip of guide.summary.slice(0, 3); track tip) {
                      <li>{{ tip }}</li>
                    }
                  </ul>
                }
                <ul class="pages">
                  @for (issue of group.issues.slice(0, 15); track issue.entityId) {
                    <li>{{ issue.pageUrl || 'Site geneli' }}</li>
                  }
                  @if (group.issues.length > 15) {
                    <li class="more">+ {{ group.issues.length - 15 }} sayfa daha</li>
                  }
                </ul>
                @if (group.docUrl) {
                  <a class="doc" [href]="group.docUrl" target="_blank" rel="noopener">Google dokümantasyonu →</a>
                }
              </div>
            </details>
          }
        </div>
      }
    </section>
  `,
  styles: [`
    .critical-report {
      margin: var(--space-4) 0;
      padding: var(--space-4);
      border-radius: 8px;
      border: 1px solid #e8e8e8;
      background: #fafafa;
    }
    .critical-report.has-critical {
      border-color: #f5c6cb;
      background: #fff5f5;
    }
    .critical-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: var(--space-3);
      flex-wrap: wrap;
      margin-bottom: var(--space-3);
    }
    .critical-header h3 { margin: 0 0 0.35rem; font-size: 1.05rem; }
    .critical-summary, .critical-ok { margin: 0; font-size: 0.85rem; color: #555; }
    .critical-ok { color: #155724; }
    .new-badge { color: #c0392b; font-weight: 600; }
    .critical-groups { display: flex; flex-direction: column; gap: 0.5rem; }
    .critical-item {
      border: 1px solid #f0d0d0;
      border-radius: 6px;
      background: #fff;
    }
    .critical-item summary {
      cursor: pointer;
      padding: 0.6rem 0.75rem;
      list-style: none;
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.88rem;
    }
    .critical-item summary::-webkit-details-marker { display: none; }
    .pill {
      font-size: 0.7rem;
      padding: 0.1rem 0.45rem;
      border-radius: 4px;
      font-weight: 600;
    }
    .pill.rule { background: #fdecea; color: #922b21; }
    .pill.new { background: #c0392b; color: #fff; }
    .msg { flex: 1 1 12rem; font-weight: 500; }
    .meta { color: #888; font-size: 0.75rem; }
    .critical-body { padding: 0 0.75rem 0.75rem; font-size: 0.85rem; }
    .fix { margin: 0 0 0.5rem; color: #444; }
    .tips { margin: 0 0 0.5rem 1.1rem; color: #555; }
    .pages { margin: 0; padding-left: 1.1rem; font-size: 0.8rem; color: #333; }
    .pages .more { color: #888; font-style: italic; }
    .doc { font-size: 0.8rem; display: inline-block; margin-top: 0.35rem; }
  `],
})
export class CriticalIssuesReportComponent {
  issues = input.required<AuditIssueDto[]>();
  criticalCount = input(0);
  completed = input(false);
  canExport = input(false);

  exportClicked = output<void>();

  protected readonly hints = AUDIT_HINTS;
  protected readonly categoryLabel = categoryLabel;

  criticalGroups = computed(() => groupCriticalIssues(this.issues()));

  distinctRuleCount = computed(() => {
    const ids = new Set(
      this.issues().filter((i) => i.severity === 'Critical').map((i) => i.ruleId),
    );
    return ids.size;
  });

  affectedPageCount = computed(() => {
    const pages = new Set(
      this.issues()
        .filter((i) => i.severity === 'Critical')
        .map((i) => i.pageUrl?.trim() || '__site__'),
    );
    return pages.size;
  });

  newRuleIds = computed(() => extractNewCriticalRuleIds(this.issues()));

  playbookFor(group: IssueGroup): RulePlaybookEntry | null {
    return getRulePlaybookOrFallback({
      ruleId: group.ruleId,
      message: group.label,
      fixHint: group.fixHint,
      docUrl: group.docUrl,
    });
  }
}
