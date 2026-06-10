const DEFAULT_TIMEOUT_MS = Number(process.env.FETCH_TIMEOUT_MS ?? "30000");
const MAX_RETRIES = Number(process.env.FETCH_MAX_RETRIES ?? "2");

export async function fetchWithRetry(url: string, init: RequestInit = {}): Promise<Response> {
  let lastError: Error | undefined;

  for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
    try {
      return await fetch(url, {
        ...init,
        signal: AbortSignal.timeout(DEFAULT_TIMEOUT_MS),
      });
    } catch (err) {
      lastError = err instanceof Error ? err : new Error(String(err));
      if (attempt < MAX_RETRIES) {
        await new Promise((resolve) => setTimeout(resolve, 500 * (attempt + 1)));
      }
    }
  }

  throw lastError ?? new Error("fetch failed");
}
