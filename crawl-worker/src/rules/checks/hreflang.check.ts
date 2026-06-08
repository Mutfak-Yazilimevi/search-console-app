import type { SeoRule } from "../rule-loader.js";
import type { CrawlIssue } from "../issue-factory.js";
import { makeIssue } from "../issue-factory.js";
import { evidenceCctldHint, evidenceHreflangGraph } from "../page-evidence.js";

export interface HreflangEntry {
  pageUrl: string;
  lang: string;
  targetUrl: string;
}

function normalizeUrl(url: string): string {
  return url.replace(/\/$/, "") || url;
}

export function checkHreflangGraph(
  entries: HreflangEntry[],
  crawledUrls: Set<string>,
  rules: Map<string, SeoRule>,
): CrawlIssue[] {
  if (entries.length < 2) return [];

  let missingReturn = 0;
  const examples: string[] = [];

  for (const entry of entries) {
    const targetNorm = normalizeUrl(entry.targetUrl);
    if (!crawledUrls.has(targetNorm)) continue;

    const targetEntries = entries.filter((e) => normalizeUrl(e.pageUrl) === targetNorm);
    const pageNorm = normalizeUrl(entry.pageUrl);
    const hasReturn = targetEntries.some((e) => normalizeUrl(e.targetUrl) === pageNorm);

    if (!hasReturn) {
      missingReturn++;
      if (examples.length < 5) {
        examples.push(`${entry.lang}: ${entry.pageUrl} → ${entry.targetUrl}`);
      }
    }
  }

  if (missingReturn >= 2) {
    const rule = rules.get("INTL-001");
    if (rule) {
      return [makeIssue(rule, evidenceHreflangGraph(missingReturn, examples))];
    }
  }

  return [];
}

export function detectCctldHint(
  origin: string,
  entries: HreflangEntry[],
  rules: Map<string, SeoRule>,
): CrawlIssue[] {
  const host = new URL(origin).hostname.toLowerCase();
  const tld = host.split(".").pop() ?? "";
  const genericTlds = new Set(["com", "org", "net", "gov", "edu", "io", "app", "dev", "ai"]);
  const isCctld = tld.length === 2 && !genericTlds.has(tld);
  if (!isCctld || entries.length < 2) return [];

  const rule = rules.get("INTL-002");
  if (rule) return [makeIssue(rule, evidenceCctldHint(host))];
  return [];
}
