import type { RobotsTxt } from "./robots-sitemap.js";

const CSS_JS_PATTERNS = [
  /\/css\b/i,
  /\/js\b/i,
  /\/styles?\b/i,
  /\/scripts?\b/i,
  /\.css/i,
  /\.js\b/i,
  /\/static\//i,
  /\/assets\//i,
];

export function findBlockedCssJsRules(robots: RobotsTxt): string[] {
  return robots.disallowed.filter((rule) =>
    CSS_JS_PATTERNS.some((p) => p.test(rule)),
  );
}

export function parseRobotsSyntaxWarnings(text: string): string[] {
  const warnings: string[] = [];
  if (!text.trim()) return warnings;

  let hasUserAgent = false;
  for (const line of text.split("\n")) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;
    if (/^user-agent\s*:/i.test(trimmed)) hasUserAgent = true;
    if (/^disallow\s*:\s*$/i.test(trimmed)) {
      warnings.push("Boş Disallow satırı");
    }
    if (/^[^:]+$/.test(trimmed) && !trimmed.startsWith("#")) {
      warnings.push(`Geçersiz satır: ${trimmed.slice(0, 40)}`);
    }
  }
  if (!hasUserAgent) warnings.push("User-agent direktifi yok");

  return [...new Set(warnings)].slice(0, 5);
}

export async function testGooglebotAccess(
  url: string,
  normalStatus: number,
): Promise<{ blocked: boolean; googlebotStatus: number | null }> {
  try {
    const response = await fetch(url, {
      headers: {
        "User-Agent":
          "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)",
      },
      redirect: "follow",
    });
    const gbStatus = response.status;
    const blocked =
      normalStatus >= 200 &&
      normalStatus < 400 &&
      (gbStatus >= 400 || gbStatus === 403);
    return { blocked, googlebotStatus: gbStatus };
  } catch {
    return { blocked: false, googlebotStatus: null };
  }
}
