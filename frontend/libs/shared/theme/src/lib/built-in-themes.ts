import { Theme } from './theme.model';

/**
 * Built-in default temalar. Müşteriye özel temalar `assets/themes/*.json`
 * altında veya backend'den dinamik gelir.
 *
 * Boot zamanında en azından default light yüklenir → uygulama renksiz kalmaz.
 */
export const DEFAULT_LIGHT_THEME: Theme = {
  name: 'default-light',
  displayName: 'Varsayılan Açık',
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
  fonts: {
    sans: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
    mono: 'ui-monospace, "SF Mono", Menlo, monospace',
  },
  radius: { sm: '0.25rem', md: '0.5rem', lg: '0.75rem', full: '9999px' },
  shadow: {
    sm: '0 1px 2px 0 rgb(0 0 0 / 0.05)',
    md: '0 4px 6px -1px rgb(0 0 0 / 0.1)',
    lg: '0 10px 15px -3px rgb(0 0 0 / 0.1)',
  },
};

export const DEFAULT_DARK_THEME: Theme = {
  name: 'default-dark',
  displayName: 'Varsayılan Koyu',
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
  fonts: DEFAULT_LIGHT_THEME.fonts,
  radius: DEFAULT_LIGHT_THEME.radius,
  shadow: {
    sm: '0 1px 2px 0 rgb(0 0 0 / 0.3)',
    md: '0 4px 6px -1px rgb(0 0 0 / 0.4)',
    lg: '0 10px 15px -3px rgb(0 0 0 / 0.5)',
  },
};

export const BUILT_IN_THEMES: Theme[] = [DEFAULT_LIGHT_THEME, DEFAULT_DARK_THEME];
