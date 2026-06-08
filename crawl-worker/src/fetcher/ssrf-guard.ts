/** SSRF koruması — private IP, localhost ve metadata endpoint engeli. */
const PRIVATE_IPV4 =
  /^(127\.|10\.|192\.168\.|169\.254\.|0\.|100\.(6[4-9]|[7-9]\d|1[01]\d|12[0-7])\.)/;

export function isBlockedHost(hostname: string): boolean {
  const h = hostname.toLowerCase().replace(/^\[|\]$/g, "");
  if (h === "localhost" || h.endsWith(".localhost")) return true;
  if (h === "::1" || h === "0:0:0:0:0:0:0:1") return true;
  if (h.includes("metadata.google.internal")) return true;
  if (PRIVATE_IPV4.test(h)) return true;
  if (/^172\.(1[6-9]|2\d|3[01])\./.test(h)) return true;
  return false;
}

export function assertSafeFetchUrl(url: string, allowedOrigin?: string): void {
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    throw new Error(`Geçersiz URL: ${url}`);
  }

  if (parsed.protocol !== "http:" && parsed.protocol !== "https:") {
    throw new Error(`Desteklenmeyen protokol: ${parsed.protocol}`);
  }

  if (isBlockedHost(parsed.hostname)) {
    throw new Error(`Güvenlik: engellenen hedef (${parsed.hostname})`);
  }

  if (allowedOrigin) {
    const allowed = new URL(allowedOrigin).origin;
    if (parsed.origin !== allowed) {
      throw new Error(`Güvenlik: crawl yalnızca ${allowed} içinde (${parsed.origin})`);
    }
  }
}
