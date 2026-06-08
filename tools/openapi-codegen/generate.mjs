// @ts-check
/**
 * Backend Swagger endpoint'lerinden TypeScript tipleri üretir.
 *
 * Kullanım:
 *   1. Backend'i çalıştır: dotnet run --project src/SearchConsoleApp.Web
 *   2. node tools/openapi-codegen/generate.mjs
 *
 * Çıktı:
 *   frontend/libs/shared/models/src/lib/generated.ts
 *   mobile/app/src/models/generated.ts
 *
 * Bu dosyalar git'te tutulur — CI'da regenerate edilip diff kontrolü yapılır
 * (backend kontrat değiştiyse PR'da fark görünür).
 */

import { execSync } from 'node:child_process';
import { writeFileSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, '..', '..');

const BACKEND_URL = process.env.BACKEND_URL ?? 'http://localhost:5000';
// Swagger doc grupları — version-aware (Asp.Versioning ile)
const SWAGGER_DOCS = ['public', 'web', 'admin'];

const TARGETS = [
  {
    name: 'frontend',
    path: join(ROOT, 'frontend', 'libs', 'shared', 'models', 'src', 'lib', 'generated.ts'),
  },
  {
    name: 'mobile',
    path: join(ROOT, 'mobile', 'app', 'src', 'models', 'generated.ts'),
  },
];

const HEADER = `/* eslint-disable */
/**
 * BU DOSYA OTOMATİK ÜRETİLİR — ELLE DEĞİŞTİRME.
 * Üretmek için: node tools/openapi-codegen/generate.mjs
 *
 * Backend: ${BACKEND_URL}
 * Üretim zamanı: ${new Date().toISOString()}
 */
`;

async function generateForDoc(docName) {
  const url = `${BACKEND_URL}/swagger/${docName}/swagger.json`;
  console.log(`[${docName}] fetching ${url}…`);

  // openapi-typescript CLI çağrısı
  const output = execSync(
    `npx openapi-typescript ${url} --root-types --root-types-no-schema-prefix`,
    { cwd: __dirname, encoding: 'utf8', maxBuffer: 50 * 1024 * 1024 }
  );

  // Namespace ile sarmala — public/web/admin tipleri çakışmasın
  const ns = docName[0].toUpperCase() + docName.slice(1);
  return `\n// ============= /api/${docName}/* =============\nexport namespace ${ns}Api {\n${indent(output, 2)}\n}\n`;
}

function indent(str, spaces) {
  const pad = ' '.repeat(spaces);
  return str
    .split('\n')
    .map(l => (l ? pad + l : l))
    .join('\n');
}

async function main() {
  let combined = HEADER;

  for (const doc of SWAGGER_DOCS) {
    try {
      combined += await generateForDoc(doc);
    } catch (err) {
      console.error(`[${doc}] HATA:`, err.message);
      console.error('Backend çalışıyor mu? BACKEND_URL doğru mu?');
      process.exit(1);
    }
  }

  // Her target'a yaz
  for (const target of TARGETS) {
    mkdirSync(dirname(target.path), { recursive: true });
    writeFileSync(target.path, combined, 'utf8');
    console.log(`✓ ${target.name}: ${target.path}`);
  }

  console.log('\nDone. generated.ts dosyaları güncellendi.');
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
