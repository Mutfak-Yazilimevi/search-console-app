export interface OAuthSetupLink {
  label: string;
  url: string;
}

export interface OAuthSetupStep {
  order: number;
  title: string;
  detail: string;
}

export interface OAuthConfigKeyHint {
  appsettingsKey: string;
  envVariable: string;
  description: string;
  configured: boolean;
}

export interface OAuthSetupGuide {
  code: string;
  provider: string;
  purpose: string;
  title: string;
  summary: string;
  steps: OAuthSetupStep[];
  links: OAuthSetupLink[];
  configKeys: OAuthConfigKeyHint[];
  redirectUri?: string | null;
  envFileHint?: string | null;
}

interface ProblemBody {
  title?: string;
  detail?: string;
  code?: string;
  setupGuide?: RawSetupGuide;
}

interface RawSetupGuide {
  code?: string;
  provider?: string;
  purpose?: string;
  title?: string;
  summary?: string;
  steps?: { order: number; title: string; detail: string }[];
  links?: { label: string; url: string }[];
  configKeys?: { appsettingsKey: string; envVariable: string; description: string; configured: boolean }[];
  redirectUri?: string | null;
  envFileHint?: string | null;
}

function normalizeGuide(raw: RawSetupGuide): OAuthSetupGuide {
  return {
    code: raw.code ?? 'oauth_config_missing',
    provider: raw.provider ?? 'google',
    purpose: raw.purpose ?? 'login',
    title: raw.title ?? 'OAuth yapılandırması eksik',
    summary: raw.summary ?? '',
    steps: (raw.steps ?? []).slice().sort((a, b) => a.order - b.order),
    links: raw.links ?? [],
    configKeys: raw.configKeys ?? [],
    redirectUri: raw.redirectUri,
    envFileHint: raw.envFileHint,
  };
}

export function parseOAuthSetupError(err: unknown): OAuthSetupGuide | null {
  const body = (err as { error?: ProblemBody })?.error;
  if (!body) return null;
  if (body.setupGuide) return normalizeGuide(body.setupGuide);
  if (body.code === 'oauth_config_missing' && body.setupGuide) {
    return normalizeGuide(body.setupGuide);
  }
  return null;
}

export function oauthErrorMessage(err: unknown, fallback: string): string {
  const body = (err as { error?: ProblemBody })?.error;
  return body?.title ?? body?.detail ?? fallback;
}
