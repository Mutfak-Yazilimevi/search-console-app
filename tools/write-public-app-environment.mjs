#!/usr/bin/env node
/**
 * public-app build-time environment dosyası yazar.
 *
 * Kullanım:
 *   PUBLIC_API_URL=https://api.example.com/api/v1 node tools/write-public-app-environment.mjs github-pages
 */
import { writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const targets = new Set(['github-pages', 'production']);
const target = process.argv[2];

if (!targets.has(target)) {
  console.error('Usage: PUBLIC_API_URL=... node tools/write-public-app-environment.mjs <github-pages|production>');
  process.exit(1);
}

const apiRootUrl = process.env.PUBLIC_API_URL?.trim();
if (!apiRootUrl) {
  console.error('PUBLIC_API_URL environment variable is required');
  process.exit(1);
}

const safeUrl = apiRootUrl.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
const content = `export const environment = {
  production: true,
  apiRootUrl: '${safeUrl}',
  defaultAudience: 'public' as const,
  defaultTheme: 'default-light',
};
`;

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const file = join(
  root,
  'frontend',
  'apps',
  'public-app',
  'src',
  'environments',
  `environment.${target}.ts`,
);

writeFileSync(file, content, 'utf8');
console.log(`Wrote ${file}`);
