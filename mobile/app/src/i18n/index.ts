import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import { getLocales } from 'expo-localization';
import en from '@/locales/en.json';
import tr from '@/locales/tr.json';

/**
 * i18n bootstrap. App.tsx'in en üstünde bir kez çağrılır.
 * Cihazın diline göre otomatik seçilir; kullanıcı manuel override edebilir.
 */
export function initI18n(defaultLocale = 'en'): void {
  const deviceLocale = getLocales()[0]?.languageCode ?? defaultLocale;

  void i18n.use(initReactI18next).init({
    resources: {
      en: { translation: en },
      tr: { translation: tr },
    },
    lng: deviceLocale,
    fallbackLng: defaultLocale,
    interpolation: { escapeValue: false }, // React zaten escape ediyor
    compatibilityJSON: 'v4',
  });
}

export { default as i18n } from 'i18next';
