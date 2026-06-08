import * as Sentry from '@sentry/react-native';

/**
 * App.tsx'in en başında çağrılır. DSN null ise no-op.
 * Sentry sadece production'da aktif olur — dev'de gürültü yapmaz.
 */
export function initSentry(dsn: string | null, production: boolean): void {
  if (!dsn || !production) return;

  Sentry.init({
    dsn,
    tracesSampleRate: 0.2,
    enableNativeCrashHandling: true,
  });
}

export { Sentry };
