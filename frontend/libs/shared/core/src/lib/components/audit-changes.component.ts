import { Component, ChangeDetectionStrategy, input, computed } from '@angular/core';
import { CommonModule } from '@angular/common';

interface FieldChange {
  field: string;
  old: unknown;
  new: unknown;
}

/**
 * AuditLog.ChangesJson içeriğini görsel diff olarak gösterir.
 *
 * Beklenen format:
 *   { "Email": { "old": "x@y.com", "new": "z@y.com" }, ... }
 *
 * Hassas alanlar backend'de maskelendiği için "***" görünür — frontend
 * ekstra bir şey yapmaz.
 */
@Component({
  selector: 'SearchConsoleApp-audit-changes',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (changes().length === 0) {
      <span class="muted">değişiklik yok</span>
    } @else {
      <table class="diff-table">
        <thead>
          <tr><th>Alan</th><th>Eski</th><th>Yeni</th></tr>
        </thead>
        <tbody>
          @for (c of changes(); track c.field) {
            <tr>
              <td class="field">{{ c.field }}</td>
              <td class="old">{{ format(c.old) }}</td>
              <td class="new">{{ format(c.new) }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [`
    .diff-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    .diff-table th, .diff-table td {
      padding: 0.25rem 0.5rem; border-bottom: 1px solid var(--color-border);
      text-align: left;
    }
    .diff-table th { color: var(--color-text-muted); font-weight: 500; }
    .field { font-family: monospace; color: var(--color-text-muted); }
    .old { color: var(--color-danger); text-decoration: line-through; opacity: 0.7; }
    .new { color: var(--color-success); }
    .muted { color: var(--color-text-muted); font-style: italic; }
  `]
})
export class AuditChangesComponent {
  readonly json = input<string | null | undefined>(null);

  readonly changes = computed<FieldChange[]>(() => {
    const raw = this.json();
    if (!raw) return [];
    try {
      const parsed = JSON.parse(raw) as Record<string, { old: unknown; new: unknown }>;
      return Object.entries(parsed).map(([field, vals]) => ({
        field,
        old: vals.old,
        new: vals.new
      }));
    } catch {
      return [];
    }
  });

  format(value: unknown): string {
    if (value === null || value === undefined) return '(boş)';
    if (typeof value === 'object') return JSON.stringify(value);
    return String(value);
  }
}
