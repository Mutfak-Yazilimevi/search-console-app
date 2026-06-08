import type { Theme } from './tokens';

export const DEFAULT_LIGHT: Theme = {
  name: 'default-light',
  displayName: 'Default Light',
  mode: 'light',
  colors: {
    primary: '#2563eb',
    primaryHover: '#1d4ed8',
    primaryActive: '#1e40af',
    primaryForeground: '#ffffff',
    success: '#16a34a',
    warning: '#ea580c',
    danger: '#dc2626',
    info: '#0891b2',
    background: '#ffffff',
    surface: '#f8fafc',
    surfaceElevated: '#ffffff',
    text: '#0f172a',
    textMuted: '#475569',
    textSubtle: '#94a3b8',
    border: '#e2e8f0',
    borderStrong: '#cbd5e1',
  },
  radius: { sm: 4, md: 8, lg: 12, full: 9999 },
};

export const DEFAULT_DARK: Theme = {
  name: 'default-dark',
  displayName: 'Default Dark',
  mode: 'dark',
  colors: {
    primary: '#3b82f6',
    primaryHover: '#60a5fa',
    primaryActive: '#2563eb',
    primaryForeground: '#ffffff',
    success: '#22c55e',
    warning: '#f97316',
    danger: '#ef4444',
    info: '#06b6d4',
    background: '#0a0a0a',
    surface: '#171717',
    surfaceElevated: '#262626',
    text: '#fafafa',
    textMuted: '#a3a3a3',
    textSubtle: '#737373',
    border: '#262626',
    borderStrong: '#404040',
  },
  radius: { sm: 4, md: 8, lg: 12, full: 9999 },
};

export const BUILT_IN_THEMES: Theme[] = [DEFAULT_LIGHT, DEFAULT_DARK];

/**
 * Web tarafındaki JSON formatından mobile Theme'e dönüştürür.
 * radius değerleri "0.5rem" gibi CSS string olabilir, number'a çevrilir.
 */
export function loadThemeFromJson(json: Record<string, unknown>): Theme {
  const radius = (json.radius ?? {}) as Record<string, unknown>;
  return {
    name: String(json.name),
    displayName: String(json.displayName),
    mode: (json.mode as 'light' | 'dark') ?? 'light',
    colors: json.colors as Theme['colors'],
    radius: {
      sm: parseCss(radius.sm) ?? 4,
      md: parseCss(radius.md) ?? 8,
      lg: parseCss(radius.lg) ?? 12,
      full: parseCss(radius.full) ?? 9999,
    },
  };
}

function parseCss(v: unknown): number | undefined {
  if (typeof v === 'number') return v;
  if (typeof v !== 'string') return undefined;
  const num = parseFloat(v);
  if (isNaN(num)) return undefined;
  if (v.includes('rem') || v.includes('em')) return num * 16;
  return num;
}
