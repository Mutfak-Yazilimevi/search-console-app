import { Component, input, ChangeDetectionStrategy } from '@angular/core';

/**
 * Küçük "i" ikonu — üzerine gelince veya odaklanınca açıklama gösterir.
 */
@Component({
  selector: 'SearchConsoleApp-hint',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="hint" [attr.aria-label]="text()" role="note" tabindex="0">
      <span class="hint-icon" aria-hidden="true">i</span>
      <span class="hint-tooltip">{{ text() }}</span>
    </span>
  `,
  styles: [`
    .hint {
      position: relative;
      display: inline-flex;
      vertical-align: middle;
      margin-left: 0.2rem;
      outline: none;
    }
    .hint-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 1rem;
      height: 1rem;
      border-radius: 50%;
      background: var(--color-surface-2, #e8ecf0);
      color: var(--color-text-muted, #556);
      font-size: 0.65rem;
      font-weight: 700;
      font-style: italic;
      font-family: Georgia, 'Times New Roman', serif;
      cursor: help;
      line-height: 1;
      flex-shrink: 0;
    }
    .hint:hover .hint-icon,
    .hint:focus-visible .hint-icon {
      background: var(--color-primary, #4285f4);
      color: var(--color-primary-foreground, #fff);
    }
    .hint-tooltip {
      visibility: hidden;
      opacity: 0;
      position: absolute;
      z-index: 100;
      bottom: calc(100% + 6px);
      left: 50%;
      transform: translateX(-50%);
      width: max-content;
      max-width: min(280px, 85vw);
      padding: 0.5rem 0.65rem;
      background: #1e293b;
      color: #f1f5f9;
      font-size: 0.75rem;
      font-weight: 400;
      font-style: normal;
      font-family: system-ui, sans-serif;
      line-height: 1.45;
      text-align: left;
      border-radius: 6px;
      box-shadow: 0 4px 14px rgba(0, 0, 0, 0.18);
      pointer-events: none;
      transition: opacity 120ms ease, visibility 120ms ease;
    }
    .hint-tooltip::after {
      content: '';
      position: absolute;
      top: 100%;
      left: 50%;
      transform: translateX(-50%);
      border: 5px solid transparent;
      border-top-color: #1e293b;
    }
    .hint:hover .hint-tooltip,
    .hint:focus-visible .hint-tooltip {
      visibility: visible;
      opacity: 1;
    }
  `],
})
export class HintIconComponent {
  text = input.required<string>();
}
