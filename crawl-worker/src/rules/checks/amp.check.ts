import type { SeoRule } from "../rule-loader.js";
import type { CrawlIssue } from "../issue-factory.js";
import { makeIssue } from "../issue-factory.js";
import { evidenceAmpError } from "../page-evidence.js";

export async function checkAmpHtmlLink(
  ampUrl: string,
  pageUrl: string,
  allowedOrigin: string,
  rules: Map<string, SeoRule>,
): Promise<CrawlIssue[]> {
  const issues: CrawlIssue[] = [];
  try {
    const resolved = new URL(ampUrl, pageUrl).href;
    const response = await fetch(resolved, {
      method: "HEAD",
      headers: { "User-Agent": "SearchConsoleApp-CrawlWorker/1.0" },
      redirect: "follow",
    });
    if (response.status >= 400) {
      const rule = rules.get("AMP-001");
      if (rule) issues.push(makeIssue(rule, evidenceAmpError(resolved, response.status)));
    }
  } catch {
    const rule = rules.get("AMP-001");
    if (rule) issues.push(makeIssue(rule, evidenceAmpError(ampUrl)));
  }
  return issues;
}
