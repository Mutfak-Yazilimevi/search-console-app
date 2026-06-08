import { IntegrationItemDto } from '../audit/audit-api.service';

export const GMC_INTEGRATION_IDS = new Set([
  'crawl-worker',
  'pagespeed',
  'safe-browsing',
  'gemini',
  'llm-eeat',
  'merchant-center-oauth',
]);

export function filterGmcIntegrations(integrations: IntegrationItemDto[]): IntegrationItemDto[] {
  return integrations.filter((i) => GMC_INTEGRATION_IDS.has(i.id));
}
