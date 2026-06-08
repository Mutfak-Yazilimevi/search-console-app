import { useEffect } from 'react';
import { useTheme } from './theme-provider';
import { loadThemeFromJson } from './themes';
import { useApiClient } from '@/api/api-client-provider';

interface ThemeListItem {
  name: string;
  displayName: string;
  mode: string;
}

/**
 * Backend'den aktif temaları çekip ThemeProvider'a kaydeder.
 * App boot'unda bir kez çağrılır — örn. App.tsx içinde useEffect ile.
 *
 * Multi-tenant senaryosunda her tenant'ın kendi temaları gelir
 * (backend tenant'ı subdomain/JWT'den çözer).
 */
export function useRemoteThemes(autoLoad = true) {
  const client = useApiClient();
  const { registerThemes, setTheme, theme } = useTheme();

  useEffect(() => {
    if (!autoLoad) return;
    let cancelled = false;

    (async () => {
      try {
        // 1. Liste çek
        const list = await client.get<ThemeListItem[]>('public', 'themes');
        if (cancelled || list.length === 0) return;

        // 2. Her temanın detayını paralel çek
        const themes = await Promise.all(
          list.map(meta =>
            client.get<Record<string, unknown>>('public', `themes/${meta.name}`)
              .then(loadThemeFromJson)
              .catch(() => null)
          )
        );

        if (cancelled) return;

        const loaded = themes.filter((t): t is NonNullable<typeof t> => t != null);
        registerThemes(loaded);
      } catch {
        // Backend ulaşılamıyor → built-in temalar kullanılır, sessiz fallback
      }
    })();

    return () => { cancelled = true; };
  }, [autoLoad, client, registerThemes]);

  return { current: theme, setTheme };
}
