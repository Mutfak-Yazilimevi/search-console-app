import { createContext, useContext, useState, useCallback, useEffect, useMemo, type PropsWithChildren } from 'react';
import { useColorScheme } from 'react-native';
import { defaultSecureStorage, type SecureStorage } from '@/utils/secure-storage';
import { BUILT_IN_THEMES, DEFAULT_LIGHT, DEFAULT_DARK } from './themes';
import type { Theme } from './tokens';
import { tokens } from './tokens';

const THEME_KEY = 'SearchConsoleApp.theme.name';
const THEME_MODE_KEY = 'SearchConsoleApp.theme.mode';  // 'system' | 'light' | 'dark' | name

interface ThemeContextValue {
  theme: Theme;
  tokens: typeof tokens;
  /** Manuel tema seç (kullanıcı dropdown'dan) */
  setTheme: (theme: Theme) => Promise<void>;
  /** Sistem dark mode'unu izle */
  setSystemMode: () => Promise<void>;
  availableThemes: Theme[];
  registerThemes: (themes: Theme[]) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

export interface ThemeProviderProps {
  /** Built-in'lere ek olarak başlangıçta kayıtlı yapılacak temalar */
  themes?: Theme[];
  /** Default tema adı */
  defaultThemeName?: string;
  /** Test için override */
  storage?: SecureStorage;
}

export function ThemeProvider({
  themes = [],
  defaultThemeName = DEFAULT_LIGHT.name,
  storage = defaultSecureStorage,
  children,
}: PropsWithChildren<ThemeProviderProps>) {
  const systemScheme = useColorScheme();
  const [available, setAvailable] = useState<Theme[]>([...BUILT_IN_THEMES, ...themes]);
  const [followSystem, setFollowSystem] = useState(true);
  const [manualTheme, setManualTheme] = useState<Theme | null>(null);

  // Persist edilmiş seçimi yükle
  useEffect(() => {
    void (async () => {
      const savedMode = await storage.getItem(THEME_MODE_KEY);
      if (savedMode === 'manual') {
        const savedName = await storage.getItem(THEME_KEY);
        const found = available.find(t => t.name === savedName);
        if (found) {
          setManualTheme(found);
          setFollowSystem(false);
        }
      } else {
        setFollowSystem(true);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Hangi tema aktif?
  const theme = useMemo<Theme>(() => {
    if (!followSystem && manualTheme) return manualTheme;
    // System mode: dark ise dark default, değilse default
    if (systemScheme === 'dark') {
      return available.find(t => t.name === DEFAULT_DARK.name) ?? DEFAULT_DARK;
    }
    return available.find(t => t.name === defaultThemeName) ?? DEFAULT_LIGHT;
  }, [followSystem, manualTheme, systemScheme, available, defaultThemeName]);

  const setTheme = useCallback(async (newTheme: Theme) => {
    setManualTheme(newTheme);
    setFollowSystem(false);
    await storage.setItem(THEME_MODE_KEY, 'manual');
    await storage.setItem(THEME_KEY, newTheme.name);
  }, [storage]);

  const setSystemMode = useCallback(async () => {
    setFollowSystem(true);
    setManualTheme(null);
    await storage.setItem(THEME_MODE_KEY, 'system');
    await storage.removeItem(THEME_KEY);
  }, [storage]);

  const registerThemes = useCallback((newThemes: Theme[]) => {
    setAvailable(prev => {
      const map = new Map(prev.map(t => [t.name, t]));
      newThemes.forEach(t => map.set(t.name, t));
      return Array.from(map.values());
    });
  }, []);

  const value = useMemo<ThemeContextValue>(
    () => ({ theme, tokens, setTheme, setSystemMode, availableThemes: available, registerThemes }),
    [theme, setTheme, setSystemMode, available, registerThemes],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

/**
 * Aktif temayı + token'ları birlikte verir.
 * Komponentler genelde sadece `theme.colors.x` ve `tokens.spacing.x` kullanır.
 *
 * Performans notu: Tema her değişimde tüm useTheme tüketicileri re-render olur.
 * Bu kabul edilebilir çünkü tema değişimi nadir bir aksiyon (kullanıcı seçimi
 * veya system dark mode geçişi). Sık değişen state'leri tema'ya KOYMA.
 */
export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error('useTheme: ThemeProvider yok.');
  return ctx;
}
