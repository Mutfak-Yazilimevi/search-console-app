/**
 * Tema renkleri. Web tarafıyla aynı şema — frontend/themes/<name>.json
 * dosyaları doğrudan tüketilebilir (rem→number çevrimi loadThemeFromJson).
 */
export interface ThemeColors {
  primary: string;
  primaryHover: string;
  primaryActive: string;
  primaryForeground: string;

  success: string;
  warning: string;
  danger: string;
  info: string;

  background: string;
  surface: string;
  surfaceElevated: string;

  text: string;
  textMuted: string;
  textSubtle: string;

  border: string;
  borderStrong: string;
}

export interface Theme {
  name: string;
  displayName: string;
  mode: 'light' | 'dark';
  colors: ThemeColors;
  radius: { sm: number; md: number; lg: number; full: number };
}

/**
 * Tema-bağımsız design tokens. Spacing, typography, shadow gibi
 * her komponentte tekrar tekrar yazılan değerler.
 *
 * Mobile'da CSS variable yok → JS objesi olarak export, useTheme ile birleştir.
 */
export const tokens = {
  spacing: {
    xs: 4,
    sm: 8,
    md: 12,
    lg: 16,
    xl: 24,
    '2xl': 32,
    '3xl': 48,
  },
  typography: {
    xs: { fontSize: 12, lineHeight: 16 },
    sm: { fontSize: 14, lineHeight: 20 },
    base: { fontSize: 16, lineHeight: 24 },
    lg: { fontSize: 18, lineHeight: 28 },
    xl: { fontSize: 20, lineHeight: 28 },
    '2xl': { fontSize: 24, lineHeight: 32 },
    '3xl': { fontSize: 30, lineHeight: 36 },
  },
  fontWeight: {
    normal: '400' as const,
    medium: '500' as const,
    semibold: '600' as const,
    bold: '700' as const,
  },
  iconSize: { sm: 16, md: 20, lg: 24, xl: 32 },
} as const;

export type Tokens = typeof tokens;
