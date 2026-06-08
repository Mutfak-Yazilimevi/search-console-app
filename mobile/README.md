# SearchConsoleApp Mobile (Expo SDK 56, Tek App)

> Mobile uygulama **opsiyoneldir**. Ana iskelet web-only çalışır. Mobile için bu klasörü kullan.

## 🏗 Yapı

Üç ayrı app YOK. **Tek app**, içinde role-based navigator:

```
mobile/app/
├── App.tsx                  # Bootstrap + provider hiyerarşisi
├── src/
│   ├── config/              # AppConfigProvider (React Context DI)
│   ├── api/                 # ApiClient + React Query
│   ├── auth/                # AuthStore + AuthProvider
│   ├── theme/               # Theme + tokens
│   ├── i18n/                # react-i18next
│   ├── locales/             # en.json, tr.json
│   ├── components/          # Button, TextField, Screen, ErrorBoundary
│   ├── navigation/          # RootNavigator (role-based!)
│   └── screens/
│       ├── public/          # Login değil → görür
│       ├── web/             # Üye girişi → görür
│       └── admin/           # Admin rolü → görür
```

## 🚀 Hızlı Başlangıç

```bash
cd mobile/app
npm install
npx expo start
```

Telefonunda **Expo Go** uygulamasını aç, QR kodu tara. Native modül gerektirmeyen her şey burada çalışır.

Native modül gerekirse (push, biometrics, custom modül):
```bash
eas build --profile development --platform ios
```

## 🎯 Role-Based Navigator

Tek bundle:
- Login değilse → `PublicNavigator` (Home, Login)
- Üye girişi → `WebNavigator` (Profile, Settings)
- Admin girişi → `AdminNavigator` (Dashboard, Customers)

Backend audience'lar (`/api/public`, `/api/web`, `/api/admin`) ApiClient method argümanı ile çağrılır.

## 📖 Detaylı Doküman

`docs/MOBILE.md` — kurulum, theme paylaşımı, native modül, production hazırlığı, yasak listesi, test pattern.
