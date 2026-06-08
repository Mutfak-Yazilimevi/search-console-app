# Mobile Reçetesi (Expo SDK 56, Tek App + Role-Based)

> Mobile uygulama **opsiyoneldir.** Ana iskelet web-only çalışır. Mobile ihtiyacı doğduğunda bu dokümanı takip et.

## Yaklaşım (v7 — Yeniden Tasarlandı)

| Konu | Karar | Sebep |
|---|---|---|
| **Framework** | Expo SDK 56 | EAS Build, OTA update, development client → bare CLI'nin tüm avantajları + tooling |
| **Kaç app?** | **TEK app**, role-based navigator | Üç ayrı app store kaydı/build/UX karmaşası yok. Kullanıcı tek "SearchConsoleApp" indirir, rolüne göre deneyim alır |
| **DI** | React Context | Singleton YOK, test edilebilir |
| **State** | `useSyncExternalStore` (auth) + `@tanstack/react-query` (server state) | Hem dependency az hem standart |
| **Forms** | `react-hook-form` + `zod` | Type-safe validation, schema-based |
| **HTTP** | `fetch` wrapper | Axios yok, ~50KB tasarruf |
| **i18n** | `react-i18next` + `expo-localization` | Cihaz dilini otomatik algıla |
| **Errors** | `AppErrorBoundary` + Sentry | App-wide crash protection |
| **Storage** | `expo-secure-store` (interface ile soyut) | iOS Keychain + Android Keystore |

## Yapı

```
mobile/app/
├── App.tsx                          # Provider hiyerarşisi + bootstrap
├── index.ts                         # registerRootComponent
├── app.json                         # Expo config
├── eas.json                         # EAS Build config
├── package.json
├── tsconfig.json
└── src/
    ├── config/
    │   └── app-config.tsx           # AppConfigProvider + useAppConfig
    ├── models/                      # DTO interface'leri (backend ile uyumlu)
    ├── utils/
    │   ├── secure-storage.ts        # SecureStorage interface + Expo impl + InMemory test impl
    │   └── sentry.ts                # Sentry init
    ├── api/
    │   ├── api-client.ts            # ApiClient (fetch wrapper, audience-aware)
    │   ├── api-client-provider.tsx  # ApiClientProvider + useApiClient
    │   ├── query-client.ts          # createQueryClient (react-query setup)
    │   └── queries.ts               # useWebMe, useAdminCustomers, vb.
    ├── auth/
    │   ├── auth-store.ts            # useSyncExternalStore tabanlı store
    │   └── auth-provider.tsx        # AuthProvider, useAuth, useAuthActions
    ├── theme/
    │   ├── tokens.ts                # spacing, typography, fontWeight, iconSize
    │   ├── themes.ts                # Theme tipi + JSON loader
    │   └── theme-provider.tsx       # ThemeProvider + useTheme (system mode dahil)
    ├── i18n/
    │   └── index.ts                 # initI18n + react-i18next setup
    ├── locales/
    │   ├── en.json
    │   └── tr.json
    ├── components/
    │   ├── button.tsx               # Button (variant, size, loading)
    │   ├── text-field.tsx           # TextField (react-hook-form Controller compatible)
    │   ├── screen.tsx               # SafeArea + KeyboardAvoiding + ScrollView wrapper
    │   └── error-boundary.tsx       # AppErrorBoundary
    ├── navigation/
    │   └── root-navigator.tsx       # PUBLIC/WEB/ADMIN stack — role-based
    └── screens/
        ├── public/
        │   ├── home-screen.tsx
        │   └── login-screen.tsx
        ├── web/
        │   ├── profile-screen.tsx
        │   └── settings-screen.tsx
        └── admin/
            ├── admin-home-screen.tsx
            └── customers-screen.tsx
```

## Role-Based Navigation (En Kritik Kavram)

```typescript
// root-navigator.tsx
if (!isAuthenticated)         → PublicNavigator (Home, Login)
else if (roles.includes('admin')) → AdminNavigator (Dashboard, Customers)
else                          → WebNavigator (Profile, Settings)
```

**Tek bundle, tek bundle ID** (`com.SearchConsoleApp.mobile`), tek App Store/Play Store kaydı. Kullanıcı:
- Login yoksa → public ekranlar görür
- Üye girişi yaparsa → web stack'e geçer
- Admin girişi yaparsa → admin stack'e geçer

Backend audience yapısı (`/api/public`, `/api/web`, `/api/admin`) korunur — ApiClient ilgili endpoint'i çağırırken audience'ı method argümanı olarak alır.

## Kurulum

```bash
cd mobile/app
npm install

# Native code'a ihtiyaç yok → Expo Go ile dene
npx expo start
# Telefonunda Expo Go uygulaması ile QR'ı tara

# Veya simulator
npx expo start --ios
npx expo start --android
```

### Custom Native Module Eklenecekse

Expo Go yeterli olmadığında **development build** yapılır (native code dahil ama EAS Build cloud'da derler — Xcode/Android Studio kurulum derdi yok):

```bash
npm install -g eas-cli
eas login
eas build:configure
eas build --profile development --platform ios
eas build --profile development --platform android
```

Dev build telefonuna yüklenir, `npx expo start --dev-client` ile bağlanır. Bare CLI'nin "native modül yazabilirim" özelliği aynen var.

## Production

```bash
# Build
eas build --profile production --platform all

# App Store + Play Store'a submit
eas submit --platform all
```

EAS otomatik halleder: code signing (iOS), keystore (Android), version bump, store gönderimi.

## App Config — Test Edilebilir DI

Singleton problem'i çözüldü. `App.tsx`'te bir kez `<AppConfigProvider config={...}>` ile sarmalanır:

```tsx
const config: AppConfig = {
  apiBaseUrl: Constants.expoConfig?.extra?.apiBaseUrl ?? 'http://localhost:5000/api',
  tokenStorageKey: 'SearchConsoleApp.mobile.token',
  sentryDsn: null,
  production: !__DEV__,
  appName: 'SearchConsoleApp',
  defaultLocale: 'en',
  defaultTheme: 'default-light',
};
```

Test'lerde:
```tsx
import { render } from '@testing-library/react-native';
render(
  <AppConfigProvider config={mockConfig}>
    <MyComponent />
  </AppConfigProvider>
);
```

## Data Fetching — React Query Pattern

Her API çağrısı için custom hook:

```typescript
// src/api/queries.ts
export function useWebMe() {
  const client = useApiClient();
  return useQuery({
    queryKey: ['web', 'me'],
    queryFn: () => client.get<Customer>('web', 'me'),
  });
}
```

Komponent kullanımı:
```tsx
const { data, isLoading, isError, refetch } = useWebMe();
```

Otomatik geliyor:
- Loading state
- Error state
- Cache (30sn staleTime, 5dk gcTime)
- Network reconnect → otomatik refetch
- 401/403/404 retry edilmez, ağ hatası 3x retry
- 401 → ApiClient `onUnauthorized` → `authStore.logout()` → RootNavigator otomatik Public'e

## Theme — System Mode + Manual

```tsx
const { theme, tokens, setTheme, setSystemMode, availableThemes } = useTheme();

// Stil
<Text style={{ color: theme.colors.text, fontSize: tokens.typography.base.fontSize }}>
```

Default davranış: **system mode** — cihaz dark mode'a geçtiğinde tema otomatik dark olur. Kullanıcı manuel seçtiğinde override edilir, persist edilir.

M�şteri başına tema (multi-tenant):
```tsx
import { loadThemeFromJson } from '@/theme/themes';

// Backend'den yükle
const json = await client.get<Record<string, unknown>>('public', `themes/${tenantSlug}`);
const customTheme = loadThemeFromJson(json);

// Theme listesine ekle
const { registerThemes, setTheme } = useTheme();
registerThemes([customTheme]);
setTheme(customTheme);
```

`frontend/themes/*.json` dosyaları doğrudan kullanılabilir → web ve mobile aynı tema dosyalarını paylaşır.

## Form Validation

```tsx
const schema = z.object({
  email: z.string().email(t('auth.emailInvalid')),
  password: z.string().min(8, t('auth.passwordTooShort')),
});

const { control, handleSubmit } = useForm<z.infer<typeof schema>>({
  resolver: zodResolver(schema),
});

<Controller
  control={control}
  name="email"
  render={({ field, fieldState }) => (
    <TextField
      label={t('auth.email')}
      value={field.value}
      onChangeText={field.onChange}
      onBlur={field.onBlur}
      error={fieldState.error?.message}
    />
  )}
/>
```

## i18n

```tsx
const { t, i18n } = useTranslation();

<Text>{t('profile.title')}</Text>
<Button onPress={() => i18n.changeLanguage('tr')}>Türkçe</Button>
```

Yeni dil eklemek:
1. `src/locales/de.json` oluştur (en.json'u kopyala, çevir).
2. `src/i18n/index.ts` içinde `resources`'a ekle.

## Emülatör Network

| Platform | localhost |
|---|---|
| iOS Simulator | `http://localhost:5000` |
| Android Emulator | `http://10.0.2.2:5000` |
| Fiziksel cihaz | LAN IP: `http://192.168.x.x:5000` |
| Expo Go (fiziksel) | LAN IP zorunlu |

Production: HTTPS zorunlu (iOS ATS).

## Push Notifications

```bash
npx expo install expo-notifications expo-device
```

`docs/MOBILE.md` ileride genişletilebilir — şimdilik hazır iskelet:

```tsx
// src/utils/push.ts (gerektiğinde ekle)
import * as Notifications from 'expo-notifications';

export async function registerForPushNotificationsAsync() {
  const { status } = await Notifications.requestPermissionsAsync();
  if (status !== 'granted') return null;
  const token = await Notifications.getExpoPushTokenAsync({ projectId: 'YOUR_EAS_PROJECT_ID' });
  return token.data;
}
```

Backend tarafında her kullanıcının push token'ını sakla, gönderirken Expo Push API kullan (veya FCM/APNs direkt).

## Deep Linking

`app.json` → `"scheme": "SearchConsoleApp"` set edildi. URL'ler:
- `SearchConsoleApp://web/profile`
- `SearchConsoleApp://admin/customers/abc-123`

React Navigation linking config eklenmesi gerekiyor — gerektiğinde:

```tsx
// root-navigator.tsx
<NavigationContainer linking={{
  prefixes: ['SearchConsoleApp://', 'https://app.SearchConsoleApp.com'],
  config: {
    screens: {
      Web: { screens: { Profile: 'profile', Settings: 'settings' }},
      Admin: { screens: { Customers: 'customers/:id?' }},
    }
  }
}}>
```

## Yasaklar

❌ **Sabit renk** komponentlerde — `theme.colors.*` kullan
❌ **Sabit spacing** — `tokens.spacing.*` kullan
❌ **`fetch` doğrudan** — `useApiClient()` üzerinden
❌ **`AsyncStorage` doğrudan** token için — `SecureStorage` abstraction
❌ **Singleton config** — `useAppConfig()` ile context'ten al
❌ **HTTP** production'da — HTTPS zorunlu
❌ **Hard-coded string** UI'da — `t('...')` kullan
❌ **Form validation** elle — react-hook-form + zod
❌ **`KeyboardAvoidingView`** elle her ekranda — `<Screen>` wrapper kullan
❌ **`useTheme()` callback dışında selector** — büyük komponent ağaçlarında her tema değişiminde re-render olur; ihtiyaç yoksa props ile geç

## Test

```bash
npm test
```

`@testing-library/react-native` ile component testleri. Mock pattern:

```tsx
function renderWithProviders(ui: React.ReactElement) {
  const mockConfig: AppConfig = { ... };
  const storage = new InMemoryStorage();
  const queryClient = createQueryClient();

  return render(
    <AppConfigProvider config={mockConfig}>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider storage={storage}>
          <AuthProvider storage={storage}>
            <ApiClientProvider storage={storage}>
              {ui}
            </ApiClientProvider>
          </AuthProvider>
        </ThemeProvider>
      </QueryClientProvider>
    </AppConfigProvider>
  );
}
```

Singleton sıfırlama derdi yok — her test taze provider tree alır.

## Önerilen Ek Kütüphaneler

| İhtiyaç | Kütüphane |
|---|---|
| Icons | `lucide-react-native` veya `@expo/vector-icons` |
| Image | `expo-image` (cache + placeholder + transition) |
| Date | `date-fns` veya `dayjs` |
| Animation | `react-native-reanimated` (zaten yüklü) + `moti` |
| Bottom sheet | `@gorhom/bottom-sheet` |
| List (büyük) | `@shopify/flash-list` (FlatList'ten 5x hızlı) |
| Camera | `expo-camera` |
| Biometrics | `expo-local-authentication` |
| Maps | `react-native-maps` |
| Stripe | `@stripe/stripe-react-native` |

## AI ile Geliştirme

`prompts/AI_PROMPT_TEMPLATE.md`'de mobile bölümü güncellendi. AI:
- `theme.colors.*` + `tokens.spacing.*` kullanır
- `useApiClient()` + `useQuery` hook'larıyla data fetcher
- `react-hook-form` + `zod` ile form
- `<Screen>` wrapper'ı kullanır
- Audience'a göre query hook ismi seçer (`useWebMe`, `useAdminCustomers`)
