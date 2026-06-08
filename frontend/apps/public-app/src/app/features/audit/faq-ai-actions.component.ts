import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { AuditAiActionsComponent, AuditAiAction } from './audit-ai-actions.component';

/** @deprecated Use SearchConsoleApp-audit-ai-actions with action="faq" */
@Component({
  selector: 'SearchConsoleApp-faq-ai-actions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AuditAiActionsComponent],
  template: `
    <SearchConsoleApp-audit-ai-actions
      [auditEntityId]="auditEntityId()"
      [pageUrl]="pageUrl()"
      action="faq" />
  `,
})
export class FaqAiActionsComponent {
  auditEntityId = input.required<string>();
  pageUrl = input.required<string>();
}
