# OpenAPI Codegen

Backend Swagger dokümanlarından TypeScript tipleri üretir. **Tek kaynak**:
backend C# kontratları → frontend + mobile TypeScript otomatik üretilir.

## Neden?

Önceden manuel sync vardı:
- Backend `LoginResponse` (C# record)
- Angular `LoginResponse` (TS interface)
- Mobile `LoginResponse` (TS interface)

Üç yerde ayrı yazıldığı için backend bir alan eklediğinde frontend ve mobile'da hatırlanması gerekiyordu. Bug kaynağı.

## Kurulum

```bash
cd tools/openapi-codegen
npm install
```

## Kullanım

```bash
# 1. Backend'i çalıştır
dotnet run --project src/SearchConsoleApp.Web

# 2. Tipleri üret (başka terminal)
cd tools/openapi-codegen
npm run generate
```

Çıktı:
- `frontend/libs/shared/models/src/lib/generated.ts`
- `mobile/app/src/models/generated.ts`

## Kullanım Yerinde

```ts
import { PublicApi, WebApi, AdminApi } from './generated';

type LoginRequest = PublicApi.components['schemas']['LoginRequest'];
type Customer = WebApi.components['schemas']['Customer'];
```

Her audience kendi namespace'inde — `Public.Customer` ile `Admin.Customer` farklı tipler olabilir (admin'e ek alanlar dönüyor olabilir).

## CI Entegrasyonu

`generated.ts` dosyaları **git'te tutulur**. CI'da regenerate edilip diff kontrol edilir:

```yaml
# .github/workflows/contract-check.yml
- run: dotnet run --project src/SearchConsoleApp.Web &
- run: sleep 5
- run: cd tools/openapi-codegen && npm install && npm run generate
- run: git diff --exit-code frontend/libs/shared/models/src/lib/generated.ts
```

Backend kontrat değiştiğinde PR'da diff görünür → frontend takımı bilinçli sync eder.

## Elle Yazılan Modeller

`generated.ts` sadece backend'in döndüğü tipleri içerir. **Frontend'e özel** tipler (UI state, form değerleri) elle yazılır:

- `frontend/libs/shared/models/src/lib/customer.model.ts` → elle, UI-friendly tipler
- `frontend/libs/shared/models/src/lib/generated.ts` → otomatik, backend kontratı

Aralarındaki farkı net tut — birini diğerine extend etme.
