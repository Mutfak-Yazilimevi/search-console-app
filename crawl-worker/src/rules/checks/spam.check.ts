import type { SeoRule } from "../rule-loader.js";
import type { CrawlIssue } from "../issue-factory.js";
import { makeIssue } from "../issue-factory.js";
import { extractBodyText } from "../html-extractors.js";
import {
  evidenceDoorwayTitles,
  evidenceSpamKeyword,
  evidenceSpamSuspiciousHost,
  evidenceSpamUgcLink,
} from "../page-evidence.js";

const SUSPICIOUS_HOSTS = [
  /doubleclick\.net/i,
  /popads\.net/i,
  /ad\.doubleclick/i,
  /clickbank/i,
  /bet365/i,
  /casino/i,
];

export function checkSpamSignals(
  url: string,
  html: string,
  origin: string,
  rules: Map<string, SeoRule>,
): CrawlIssue[] {
  const issues: CrawlIssue[] = [];

  const externalScripts = [...html.matchAll(/<script[^>]+src=["']([^"']+)["']/gi)];
  const externalIframes = [...html.matchAll(/<iframe[^>]+src=["']([^"']+)["']/gi)];
  for (const [, src] of [...externalScripts, ...externalIframes]) {
    try {
      const parsed = new URL(src, url);
      if (parsed.origin === origin) continue;
      if (SUSPICIOUS_HOSTS.some((p) => p.test(parsed.hostname))) {
        const rule = rules.get("SPAM-004");
        if (rule) {
          issues.push(makeIssue(rule, evidenceSpamSuspiciousHost(parsed.hostname, parsed.href)));
          break;
        }
      }
    } catch { /* skip */ }
  }

  const ugcBlocks = html.match(/<(?:div|section|article)[^>]+(?:class|id)=["'][^"']*(?:comment|ugc|review|forum)[^"']*["'][^>]*>[\s\S]*?<\/(?:div|section|article)>/gi) ?? [];
  for (const block of ugcBlocks) {
    const extLinks = [...block.matchAll(/<a\b[^>]+href=["'](https?:\/\/[^"']+)["']/gi)];
    for (const [, href] of extLinks) {
      try {
        if (new URL(href).origin === origin) continue;
        if (!/rel=["'][^"']*nofollow/i.test(block.slice(Math.max(0, block.indexOf(href) - 120), block.indexOf(href) + 120))) {
          const rule = rules.get("SPAM-005");
          if (rule) {
            issues.push(makeIssue(rule, evidenceSpamUgcLink(href)));
            return issues;
          }
        }
      } catch { /* skip */ }
    }
  }

  const bodyText = extractBodyText(html).toLowerCase();
  const words = bodyText.split(/\s+/).filter((w) => w.length >= 4);
  const freq = new Map<string, number>();
  for (const w of words) freq.set(w, (freq.get(w) ?? 0) + 1);
  for (const [w, c] of freq) {
    if (c >= 8 && c / Math.max(words.length, 1) > 0.04) {
      const rule = rules.get("SPAM-006");
      if (rule) {
        issues.push(makeIssue(rule, evidenceSpamKeyword(w, c, words.length)));
        break;
      }
    }
  }

  return issues;
}

export function levenshtein(a: string, b: string): number {
  const m = a.length;
  const n = b.length;
  const dp: number[] = Array.from({ length: n + 1 }, (_, i) => i);
  for (let i = 1; i <= m; i++) {
    let prev = dp[0];
    dp[0] = i;
    for (let j = 1; j <= n; j++) {
      const tmp = dp[j];
      dp[j] = a[i - 1] === b[j - 1] ? prev : 1 + Math.min(prev, dp[j], dp[j - 1]);
      prev = tmp;
    }
  }
  return dp[n];
}

export function checkDoorwayTitles(
  pages: { url: string; title: string | null }[],
  rules: Map<string, SeoRule>,
): CrawlIssue[] {
  const issues: CrawlIssue[] = [];
  const titled = pages.filter((p) => p.title && p.title.length >= 10);
  let pairs = 0;
  const examples: { url: string; title: string }[] = [];

  for (let i = 0; i < titled.length; i++) {
    for (let j = i + 1; j < titled.length; j++) {
      const a = titled[i].title!.toLowerCase();
      const b = titled[j].title!.toLowerCase();
      const dist = levenshtein(a, b);
      const maxLen = Math.max(a.length, b.length);
      if (maxLen > 0 && dist / maxLen < 0.15 && a !== b) {
        pairs++;
        if (examples.length < 5 && !examples.some((e) => e.url === titled[i].url)) {
          examples.push({ url: titled[i].url, title: titled[i].title! });
        }
      }
    }
  }
  if (pairs >= 3) {
    const rule = rules.get("SPAM-003");
    if (rule) issues.push(makeIssue(rule, evidenceDoorwayTitles(pairs, examples)));
  }
  return issues;
}
