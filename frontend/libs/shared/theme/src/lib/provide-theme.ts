import { EnvironmentProviders, makeEnvironmentProviders, provideAppInitializer, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ThemeService } from './theme.service';
import { ThemeLoaderService } from './theme-loader.service';
import { DEFAULT_LIGHT_THEME, BUILT_IN_THEMES } from './built-in-themes';
import { Theme } from './theme.model';

export interface ThemeProviderOptions {
  /** Boot zamanında uygulanacak tema. Default: 'default-light'. */
  defaultTheme?: string;

  /** Backend'den tema çekme endpoint'i, ör. http://localhost:5000/api/public/themes */
  apiThemesUrl?: string;

  /** Built-in tema listesine eklenecek inline tema'lar (kod içinde tanımlı). */
  builtInExtras?: Theme[];

  /** localStorage'da persist edilen seçimi kullan. Default: true. */
  usePersisted?: boolean;
}

/**
 * Bootstrap'te tema yüklemesini halleder:
 * 1. Persist edilmiş tema varsa onu yükler
 * 2. Yoksa defaultTheme'i yükler
 * 3. Hata olursa BUILT-IN DEFAULT_LIGHT'a düşer (uygulama renksiz kalmaz)
 */
export function provideTheme(options: ThemeProviderOptions = {}): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppInitializer(async () => {
      const theme = inject(ThemeService);
      const loader = inject(ThemeLoaderService);

      const allBuiltIn = [...BUILT_IN_THEMES, ...(options.builtInExtras ?? [])];

      // 1. Hangi tema yüklenecek?
      let target: string | null = null;
      if (options.usePersisted !== false) {
        target = theme.loadPersistedThemeName();
      }
      target ??= options.defaultTheme ?? DEFAULT_LIGHT_THEME.name;

      // 2. Built-in'lerde var mı?
      const builtIn = allBuiltIn.find(t => t.name === target);
      if (builtIn) {
        theme.apply(builtIn, false);
        return;
      }

      // 3. Network'ten yükle
      try {
        const loaded = await firstValueFrom(loader.load(target, options.apiThemesUrl));
        theme.apply(loaded, false);
      } catch (err) {
        console.warn(`Theme '${target}' yüklenemedi, default-light'a düşülüyor`, err);
        theme.apply(DEFAULT_LIGHT_THEME, false);
      }
    })
  ]);
}
