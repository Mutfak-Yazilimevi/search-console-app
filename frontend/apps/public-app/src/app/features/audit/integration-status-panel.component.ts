import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuditApiService, IntegrationItemDto } from './audit-api.service';

export type IntegrationItem = IntegrationItemDto;

@Component({
  selector: 'SearchConsoleApp-integration-status-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NgTemplateOutlet],
  template: `
    @if (items().length) {
      @if (collapsible()) {
        <details class="integration-panel" [open]="panelOpen()" (toggle)="onPanelToggle($event)">
          <summary class="panel-summary">
            <span class="summary-title">{{ title() }}</span>
            @if (summaryMeta()) {
              <span class="summary-meta">{{ summaryMeta() }}</span>
            }
            <span class="summary-chevron">{{ panelOpen() ? '▾' : '▸' }}</span>
          </summary>
          <div class="panel-body">
            <ng-container *ngTemplateOutlet="listTpl" />
          </div>
        </details>
      } @else {
        <section class="integration-panel static">
          <h4 class="panel-heading">{{ title() }}</h4>
          <ng-container *ngTemplateOutlet="listTpl" />
        </section>
      }

      <ng-template #listTpl>
        <ul class="integration-list">
          @for (item of items(); track item.id) {
            <li [class]="rowClass(item)" [class.editing]="editingId() === item.id">
              @if (editable() && item.canToggle) {
                <label class="toggle">
                  <input
                    type="checkbox"
                    [checked]="item.enabled"
                    [disabled]="savingId() === item.id"
                    (change)="onToggle(item, $event)" />
                  <span class="toggle-ui"></span>
                </label>
              } @else {
                <span class="dot"></span>
              }
              <div class="main">
                <div class="label-row">
                  <span class="label">{{ item.label }}</span>
                  @if (editable() && canEditFields(item)) {
                    <button
                      type="button"
                      class="btn-edit"
                      [disabled]="savingId() === item.id"
                      (click)="toggleEdit(item)">
                      {{ editingId() === item.id ? 'Kapat' : 'Düzenle' }}
                    </button>
                  }
                </div>
                @if (editingId() === item.id && editable() && canEditFields(item)) {
                  <div class="edit-fields">
                    @for (field of item.fields ?? []; track field.key) {
                      <label class="field-label">
                        <span>{{ field.label }}</span>
                        @if (field.hasValue && field.maskedValue && !editValues[field.key]) {
                          <span class="field-current">Mevcut: {{ field.maskedValue }}</span>
                        }
                        <input
                          [type]="field.isSecret ? 'password' : 'text'"
                          [(ngModel)]="editValues[field.key]"
                          [placeholder]="placeholderFor(field)"
                          [name]="field.key + item.id" />
                      </label>
                    }
                    <div class="edit-actions">
                      <button type="button" class="btn-save" [disabled]="savingId() === item.id" (click)="saveEdit(item)">
                        {{ savingId() === item.id ? 'Kaydediliyor…' : 'Kaydet' }}
                      </button>
                      <button type="button" class="btn-cancel" (click)="cancelEdit()">İptal</button>
                    </div>
                  </div>
                } @else {
                  @if (item.detail) {
                    <span class="detail">{{ item.detail }}</span>
                  }
                  @if (editable() && fieldPreview(item); as preview) {
                    <span class="field-preview">{{ preview }}</span>
                  }
                }
              </div>
              <span class="badge">{{ statusLabel(item.status) }}</span>
            </li>
          }
        </ul>
        @if (editHint() && editable()) {
          <p class="panel-hint">{{ editHint() }}</p>
        }
      </ng-template>
    }
  `,
  styles: [`
    .integration-panel {
      margin: 1rem 0;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      overflow: hidden;
    }
    .integration-panel.static {
      padding: 0.75rem 1rem;
    }
    .panel-summary {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      padding: 0.65rem 1rem;
      cursor: pointer;
      list-style: none;
      user-select: none;
      background: #f1f5f9;
      border-bottom: 1px solid transparent;
    }
    .integration-panel[open] .panel-summary {
      border-bottom-color: #e2e8f0;
    }
    .panel-summary::-webkit-details-marker { display: none; }
    .summary-title {
      font-size: 0.85rem;
      font-weight: 600;
      color: #334155;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    .summary-meta {
      font-size: 0.75rem;
      color: #64748b;
      font-weight: normal;
      text-transform: none;
      letter-spacing: normal;
    }
    .summary-chevron {
      margin-left: auto;
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .panel-body {
      padding: 0.5rem 1rem 0.75rem;
    }
    .panel-heading {
      margin: 0 0 0.5rem;
      font-size: 0.85rem;
      color: #475569;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    .integration-list {
      list-style: none;
      margin: 0;
      padding: 0;
      font-size: 0.82rem;
    }
    .integration-list li {
      display: grid;
      grid-template-columns: auto 1fr auto;
      gap: 0.35rem 0.65rem;
      align-items: start;
      padding: 0.45rem 0.35rem;
      border-bottom: 1px solid #f1f5f9;
      border-radius: 6px;
    }
    .integration-list li:last-child { border-bottom: none; }
    .integration-list li.editing {
      background: #fff;
      border: 1px solid #cbd5e1;
      margin: 0.15rem 0;
      padding: 0.55rem 0.45rem;
    }
    .dot {
      width: 8px; height: 8px; border-radius: 50%; background: #94a3b8;
      margin-top: 0.35rem;
    }
    .status-configured .dot, .status-connected .dot, .status-ran .dot { background: #22c55e; }
    .status-missing .dot, .status-not_configured .dot { background: #ef4444; }
    .status-skipped .dot, .status-not_connected .dot, .status-pending .dot, .status-disabled .dot { background: #f59e0b; }
    .main { min-width: 0; }
    .label-row {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .label { font-weight: 500; color: #334155; }
    .badge {
      font-size: 0.68rem; padding: 0.15rem 0.4rem; border-radius: 4px;
      background: #e2e8f0; color: #475569; white-space: nowrap; margin-top: 0.15rem;
    }
    .status-missing .badge { background: #fee2e2; color: #b91c1c; }
    .status-configured .badge, .status-connected .badge, .status-ran .badge { background: #dcfce7; color: #166534; }
    .status-disabled .badge { background: #fef3c7; color: #92400e; }
    .detail, .field-preview {
      display: block;
      color: #64748b;
      font-size: 0.75rem;
      margin-top: 0.15rem;
    }
    .field-preview {
      font-family: ui-monospace, monospace;
      color: #475569;
    }
    .btn-edit {
      font-size: 0.68rem;
      padding: 0.1rem 0.4rem;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      background: #fff;
      color: #2563eb;
      cursor: pointer;
    }
    .btn-edit:disabled { opacity: 0.5; cursor: wait; }
    .toggle {
      position: relative;
      display: inline-flex;
      align-items: center;
      margin-top: 0.2rem;
      cursor: pointer;
    }
    .toggle input {
      position: absolute;
      opacity: 0;
      width: 0;
      height: 0;
    }
    .toggle-ui {
      width: 34px;
      height: 18px;
      background: #cbd5e1;
      border-radius: 999px;
      transition: background 0.15s;
      position: relative;
    }
    .toggle-ui::after {
      content: '';
      position: absolute;
      top: 2px;
      left: 2px;
      width: 14px;
      height: 14px;
      background: #fff;
      border-radius: 50%;
      transition: transform 0.15s;
    }
    .toggle input:checked + .toggle-ui { background: #22c55e; }
    .toggle input:checked + .toggle-ui::after { transform: translateX(16px); }
    .toggle input:disabled + .toggle-ui { opacity: 0.5; }
    .edit-fields { margin-top: 0.5rem; display: flex; flex-direction: column; gap: 0.4rem; }
    .field-label { display: flex; flex-direction: column; gap: 0.15rem; font-size: 0.72rem; color: #64748b; }
    .field-current { font-size: 0.68rem; color: #94a3b8; font-family: ui-monospace, monospace; }
    .field-label input {
      padding: 0.35rem 0.5rem;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      font-size: 0.8rem;
      font-family: ui-monospace, monospace;
    }
    .field-label input:focus {
      outline: none;
      border-color: #2563eb;
      box-shadow: 0 0 0 2px rgba(37, 99, 235, 0.15);
    }
    .edit-actions { display: flex; gap: 0.4rem; margin-top: 0.25rem; }
    .btn-save, .btn-cancel {
      font-size: 0.72rem;
      padding: 0.25rem 0.55rem;
      border-radius: 4px;
      border: 1px solid #cbd5e1;
      cursor: pointer;
      background: #fff;
    }
    .btn-save { background: #2563eb; color: #fff; border-color: #2563eb; }
    .btn-save:disabled { opacity: 0.6; cursor: wait; }
    .panel-hint {
      margin: 0.5rem 0 0;
      font-size: 0.72rem;
      color: #64748b;
    }
  `],
})
export class IntegrationStatusPanelComponent {
  private auditApi = inject(AuditApiService);

  title = input('Entegrasyon durumu');
  items = input<IntegrationItemDto[]>([]);
  editable = input(false);
  collapsible = input(true);
  defaultOpen = input(false);
  editHint = input('Toggle ile adımı açıp kapatabilir, Düzenle ile API anahtarı ve URL girebilirsiniz.');
  changed = output<void>();

  panelOpen = signal(false);
  editingId = signal<string | null>(null);
  editValues: Record<string, string> = {};
  savingId = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (this.defaultOpen()) {
        this.panelOpen.set(true);
      }
    });
  }

  summaryMeta = computed(() => {
    const items = this.items();
    if (!items.length) return '';
    const missing = items.filter((i) => i.status === 'missing' || i.status === 'not_configured').length;
    const ok = items.filter((i) => ['configured', 'connected', 'ran'].includes(i.status)).length;
    const disabled = items.filter((i) => i.status === 'disabled').length;
    const parts: string[] = [];
    if (ok) parts.push(`${ok} aktif`);
    if (missing) parts.push(`${missing} eksik`);
    if (disabled) parts.push(`${disabled} kapalı`);
    return parts.join(' · ');
  });

  statusLabel(status: string): string {
    const map: Record<string, string> = {
      configured: 'Aktif',
      missing: 'Eksik',
      connected: 'Bağlı',
      not_connected: 'Bağlı değil',
      ran: 'Çalıştı',
      skipped: 'Atlandı',
      not_configured: 'Yapılandırılmadı',
      pending: 'Bekliyor',
      disabled: 'Kapalı',
    };
    return map[status] ?? status;
  }

  rowClass(item: IntegrationItemDto): string {
    return `status-${item.status}`;
  }

  canEditFields(item: IntegrationItemDto): boolean {
    return this.editable() && (item.fields?.length ?? 0) > 0;
  }

  fieldPreview(item: IntegrationItemDto): string | null {
    const configured = (item.fields ?? []).filter((f) => f.hasValue);
    if (!configured.length) return null;
    if (configured.length === 1) return configured[0].maskedValue ?? configured[0].label;
    return `${configured.length} alan yapılandırıldı`;
  }

  placeholderFor(field: { label: string; hasValue: boolean; maskedValue?: string | null }): string {
    if (field.hasValue && field.maskedValue) return 'Yeni değer girin (boş bırakılırsa değişmez)';
    return field.label;
  }

  onPanelToggle(event: Event): void {
    const el = event.target as HTMLDetailsElement;
    this.panelOpen.set(el.open);
    if (!el.open) this.cancelEdit();
  }

  toggleEdit(item: IntegrationItemDto): void {
    if (this.editingId() === item.id) {
      this.cancelEdit();
      return;
    }
    this.startEdit(item);
  }

  startEdit(item: IntegrationItemDto): void {
    this.editingId.set(item.id);
    this.editValues = {};
    for (const field of item.fields ?? []) {
      this.editValues[field.key] = '';
    }
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editValues = {};
  }

  onToggle(item: IntegrationItemDto, event: Event): void {
    const enabled = (event.target as HTMLInputElement).checked;
    this.savingId.set(item.id);
    this.auditApi.updateIntegration(item.id, { enabled }).subscribe({
      next: () => {
        this.savingId.set(null);
        this.changed.emit();
      },
      error: () => {
        this.savingId.set(null);
        (event.target as HTMLInputElement).checked = !enabled;
      },
    });
  }

  saveEdit(item: IntegrationItemDto): void {
    const values: Record<string, string> = {};
    for (const field of item.fields ?? []) {
      const v = this.editValues[field.key]?.trim();
      if (v) values[field.key] = v;
    }
    if (Object.keys(values).length === 0) return;

    this.savingId.set(item.id);
    this.auditApi.updateIntegration(item.id, { values, enabled: true }).subscribe({
      next: () => {
        this.savingId.set(null);
        this.editingId.set(null);
        this.editValues = {};
        this.changed.emit();
      },
      error: () => this.savingId.set(null),
    });
  }
}
