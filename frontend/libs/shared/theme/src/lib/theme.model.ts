/**
 * Bir tema, CSS custom property setidir.
 * Renkler hex string, ölçüler CSS string ('1rem', '4px').
 * Müşteri başına JSON dosyası olarak tutulur (themes/<name>.json).
 *
 * Runtime'da değiştirilebilir → kullanıcı yarı yolda dark mode'a geçebilir.
 */
export interface Theme {
  /** Tema adı, ör: 'default', 'customer-acme-dark' */
  name: string;

  /** Açıklayıcı isim — settings ekranında gösterilir */
  displayName: string;

  /** light / dark — color-scheme CSS property için */
  mode: 'light' | 'dark';

  /** Brand tonları — primary marka, ölçeklendirme renk paletinin bel kemiği */
  colors: {
    // Marka renkleri
    primary: string;
    primaryHover: string;
    primaryActive: string;
    primaryForeground: string;

    // Semantic
    success: string;
    warning: string;
    danger: string;
    info: string;

    // Surface (background katmanları)
    background: string;       // Sayfa zemini
    surface: string;          // Card, panel
    surfaceElevated: string;  // Dialog, popover

    // Text
    text: string;             // Ana yazı
    textMuted: string;        // İkincil yazı
    textSubtle: string;       // Placeholder

    // Border
    border: string;
    borderStrong: string;
  };

  /** Tipografi */
  fonts?: {
    sans?: string;     // Ana font ailesi
    mono?: string;     // Kod / monospace
  };

  /** Köşe yumuşaklığı — radius ölçeği */
  radius?: {
    sm?: string;
    md?: string;
    lg?: string;
    full?: string;
  };

  /** Gölge ölçeği */
  shadow?: {
    sm?: string;
    md?: string;
    lg?: string;
  };

  /** Ek custom property'ler — projeye özel ne istersen */
  extras?: Record<string, string>;
}
