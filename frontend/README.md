# SearchConsoleApp Frontend — Nx Monorepo

> **Tek workspace, 3 Angular SPA, shared lib'ler.** Bir yerde yaz, üçünde kullan. Theme runtime'da değişir, müşteri başına özelleştirilebilir.

## 🏗 Yapı

```
frontend/
├── apps/
│   ├── public-app/          # Anonim (port 4200, /api/public)
│   ├── web-app/             # Üye (port 4201, /api/web)
│   └── admin-app/           # Admin (port 4202, /api/admin)
├── libs/
│   └── shared/
│       ├── core/            # ApiClient, AuthService, interceptor, guard
│       ├── ui/              # Button, ThemeSwitcher, ortak komponentler
│       ├── theme/           # Theme model, service, loader, built-in temalar
│       ├── models/          # DTO interface'leri (backend ile uyumlu)
│       └── utils/           # Helper fonksiyonlar
├── themes/                  # Müşteri tema JSON'ları
│   ├── _index.json
│   ├── acme-light.json
│   └── ...
├── package.json
├── nx.json
└── tsconfig.base.json       # Path mapping: @SearchConsoleApp/shared/*
```

## 🔧 Teknoloji

- **Angular 21.2** (en güncel kararlı, Mayıs 2026)
- **Standalone components** (NgModule yok)
- **Signal-based state** (zone.js yok — `provideZonelessChangeDetection`)
- **OnPush** change detection — tüm komponentlerde
- **Nx 21** monorepo (incremental build, dependency graph)
- **Vitest** testing
- **TypeScript 5.9**

## 🚀 Kurulum

```bash
cd frontend
npm install
```

Üç uygulamayı paralel çalıştır (her biri ayrı terminal):
```bash
npm run start:public      # localhost:4200
npm run start:web         # localhost:4201
npm run start:admin       # localhost:4202
```

Veya birini build et:
```bash
npm run build:web
npm run build:all
```

Bağımlılık grafiğini görselleştir:
```bash
npm run graph
```

## 🧩 Shared Lib Kullanımı

Yeni service/component **shared**'a yazılır, üç app de import eder.

```typescript
// herhangi bir app'in feature'ında
import { ApiClient, AuthService } from '@SearchConsoleApp/shared/core';
import { ButtonComponent } from '@SearchConsoleApp/shared/ui';
import { ThemeService } from '@SearchConsoleApp/shared/theme';
import { Customer } from '@SearchConsoleApp/shared/models';
```

**Hangi koda hangi lib:**

| Lib | İçerik |
|---|---|
| `@SearchConsoleApp/shared/core` | `ApiClient`, `AuthService`, `authGuard`, `roleGuard`, interceptor'lar, `provideSharedCore()`, `APP_CONFIG` |
| `@SearchConsoleApp/shared/ui` | `ButtonComponent`, `ThemeSwitcherComponent`, ortak komponentler |
| `@SearchConsoleApp/shared/theme` | `Theme` model, `ThemeService`, `ThemeLoaderService`, `provideTheme()`, built-in temalar |
| `@SearchConsoleApp/shared/models` | `Customer`, `LoginRequest`, `ApiResponse<T>`, DTO interface'leri |
| `@SearchConsoleApp/shared/utils` | Helper fonksiyonlar (formatDate, slugify, vb.) |

## ⚙️ App Config Pattern

Her app'in `app.config.ts`'i **çok kısa** — sadece kendi farklarını sağlar:

```typescript
// apps/web-app/src/app/app.config.ts
provideSharedCore({
  apiBaseUrl: 'http://localhost:5000/api/web',
  tokenStorageKey: 'SearchConsoleApp.web.token',
  requiredRole: 'user',
  authEnabled: true,
  // ...
})
```

Shared core, `APP_CONFIG` injection token'ını kullanır → ApiClient, AuthService, guard'lar bu config'i okur. **Hiçbir kod tekrarı yok.**

## 🎨 Theme Sistemi

Theme = **CSS custom property** seti. Runtime'da `:root` üzerinde değişir → tüm UI anında etkilenir.

### Nasıl çalışır

1. `theme-tokens.scss` global default'ları set eder.
2. `ThemeService.apply(theme)` `:root.style.setProperty` ile override eder.
3. Komponentler `var(--color-primary)` gibi okur.
4. Seçim `localStorage`'a kaydedilir.

### Kullanıcı tema değiştirir

```html
<SearchConsoleApp-theme-switcher></SearchConsoleApp-theme-switcher>
```

Dropdown → kullanıcı seçer → anında uygulanır + persist edilir.

### Yeni bir müşteri teması ekle

`frontend/themes/customer-x.json` oluştur:

```json
{
  "name": "customer-x-light",
  "displayName": "Customer X",
  "mode": "light",
  "colors": {
    "primary": "#e91e63",
    "primaryHover": "#c2185b",
    "...": "..."
  }
}
```

`frontend/themes/_index.json`'a satır ekle. Build'de `/assets/themes/` altına kopyalanır (project.json'da assets config'i halleder).

### Programatik tema değişimi

```typescript
import { ThemeService, ThemeLoaderService } from '@SearchConsoleApp/shared/theme';

const themeService = inject(ThemeService);
const loader = inject(ThemeLoaderService);

loader.load('customer-x-light').subscribe(theme => {
  themeService.apply(theme);
});
```

### Multi-tenant: backend'den tema

Tema'yı backend'den de gönderebilirsin. `provideTheme({ apiThemesUrl: '...' })` → `GET /api/public/themes/<name>` denenir; başarısız olursa `/assets/themes/<name>.json`'a düşülür.

Tenant'a özel tema:
1. Backend `/api/public/themes/{tenantSlug}` endpoint'i ekle, DB'den çek.
2. Frontend boot'unda tenant'ın slug'ını çöz (subdomain, JWT claim).
3. `provideTheme({ defaultTheme: tenantSlug })` ile başlat.

## ✅ Sıkı Kurallar

❌ **Aynı kodu birden fazla app'te yazma** — `libs/shared/`'a koy.
❌ **`localStorage`/`sessionStorage` doğrudan kullanma** — `AuthService`/`ThemeService` üzerinden.
❌ **Component'te `fetch` veya `HttpClient` doğrudan** — sadece `ApiClient` kullan.
❌ **Sabit renk kodu yazma** — her zaman `var(--color-*)`. Aksi halde tema değişimi etki etmez.
❌ **NgModule** — sadece standalone.
❌ **Template-driven form** — yalnızca Reactive Forms.
❌ **Zone.js bağımlı kod** — zoneless mode.

## 🤖 AI ile Geliştirme

`prompts/AI_PROMPT_TEMPLATE.md`'i sistem mesajı olarak verirsen, üretilen kod:
- Hangi lib'e gideceğini bilir (`@SearchConsoleApp/shared/core` mı feature mi)
- Standalone, signal-based, OnPush yazar
- `var(--color-*)` kullanır
- `ApiClient` üzerinden HTTP yapar

## 📦 Gerçek Angular Workspace'i Oluşturma

Bu klasör Angular workspace **iskeletidir** — `node_modules` ve build artifact'leri yok. İlk kurulum:

```bash
cd frontend
npm install                       # Tüm deps yüklenir (Nx + Angular 21)
npx nx run-many -t build          # Tüm app'leri build et — config doğru mu test
```

Eğer sıfırdan Nx workspace'i kuruyorsan:

```bash
npx create-nx-workspace@21 SearchConsoleApp-frontend --preset=angular-monorepo \
  --appName=public-app --style=scss --standalone --bundler=esbuild

# Sonra web-app ve admin-app'i ekle:
npx nx g @nx/angular:app web-app  --style=scss --standalone
npx nx g @nx/angular:app admin-app --style=scss --standalone

# Lib'leri ekle:
npx nx g @nx/angular:lib shared/core   --style=scss --standalone
npx nx g @nx/angular:lib shared/ui     --style=scss --standalone
npx nx g @nx/angular:lib shared/theme  --style=scss --standalone
npx nx g @nx/angular:lib shared/models --style=scss --standalone
npx nx g @nx/angular:lib shared/utils  --style=scss --standalone
```

Sonra bu iskeletteki dosyaları yeni Nx workspace'in karşılığı klasörlerine kopyala.

## 🧪 Test

```bash
npm test                     # tüm projeler
npx nx test shared-core      # tek lib
npx nx test web-app          # tek app
```

Vitest kullanılır (Angular 21 default).
