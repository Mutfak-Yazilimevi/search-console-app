import { fetchWithRetry } from "./fetch-with-retry.js";
import { assertSafeFetchUrl } from "./ssrf-guard.js";

export interface FetchPageResult {
  statusCode: number;
  html: string;
  responseTimeMs: number;
  headers: Record<string, string>;
  redirectHops: number;
  redirectStatuses: number[];
}

export async function fetchPageWithRedirects(
  url: string,
  allowedOrigin: string,
): Promise<FetchPageResult> {
  assertSafeFetchUrl(url, allowedOrigin);
  const started = Date.now();
  let currentUrl = url;
  let redirectHops = 0;
  const redirectStatuses: number[] = [];

  for (let hop = 0; hop < 15; hop++) {
    assertSafeFetchUrl(currentUrl, allowedOrigin);
    const response = await fetchWithRetry(currentUrl, {
      headers: { "User-Agent": "SearchConsoleApp-CrawlWorker/1.0" },
      redirect: "manual",
    });

    if (response.status >= 300 && response.status < 400) {
      redirectStatuses.push(response.status);
      const location = response.headers.get("location");
      if (!location) {
        return {
          statusCode: response.status,
          html: "",
          responseTimeMs: Date.now() - started,
          headers: {},
          redirectHops,
          redirectStatuses,
        };
      }
      currentUrl = new URL(location, currentUrl).href;
      redirectHops++;
      continue;
    }

    const html = await response.text();
    const headers: Record<string, string> = {};
    response.headers.forEach((v, k) => {
      headers[k.toLowerCase()] = v;
    });
    return {
      statusCode: response.status,
      html,
      responseTimeMs: Date.now() - started,
      headers,
      redirectHops,
      redirectStatuses,
    };
  }

  return {
    statusCode: 0,
    html: "",
    responseTimeMs: Date.now() - started,
    headers: {},
    redirectHops,
    redirectStatuses,
  };
}

export function hasTemporaryRedirect(statuses: number[]): boolean {
  return statuses.some((s) => s === 302 || s === 303 || s === 307);
}

export async function checkCanonicalTarget(
  canonicalUrl: string,
  allowedOrigin: string,
): Promise<number> {
  try {
    assertSafeFetchUrl(canonicalUrl, allowedOrigin);
    const response = await fetchWithRetry(canonicalUrl, {
      method: "HEAD",
      headers: { "User-Agent": "SearchConsoleApp-CrawlWorker/1.0" },
      redirect: "follow",
    });
    return response.status;
  } catch {
    try {
      assertSafeFetchUrl(canonicalUrl, allowedOrigin);
      const response = await fetchWithRetry(canonicalUrl, {
        headers: { "User-Agent": "SearchConsoleApp-CrawlWorker/1.0" },
        redirect: "follow",
      });
      return response.status;
    } catch {
      return 0;
    }
  }
}
