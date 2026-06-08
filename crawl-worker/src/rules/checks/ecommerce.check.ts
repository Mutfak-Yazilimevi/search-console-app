import type { SeoRule } from "../rule-loader.js";
import type { CrawlIssue } from "../issue-factory.js";
import { makeIssue } from "../issue-factory.js";
import { evidenceEcommerceFacet, evidenceEcommercePagination } from "../page-evidence.js";

const FACET_PARAMS = /[?&](sort|filter|color|size|brand|price|page|facet|ref)=/i;

export function checkEcommerceUrl(url: string, html: string, rules: Map<string, SeoRule>): CrawlIssue[] {
  const issues: CrawlIssue[] = [];

  if (FACET_PARAMS.test(url)) {
    const hasNoindex = /<meta[^>]+name=["'](?:robots|googlebot)["'][^>]+content=["'][^"']*noindex/i.test(html)
      || /<meta[^>]+content=["'][^"']*noindex[^"']*["'][^>]+name=["'](?:robots|googlebot)["']/i.test(html);
    if (!hasNoindex) {
      const rule = rules.get("EC-003");
      if (rule) issues.push(makeIssue(rule, evidenceEcommerceFacet(new URL(url).search, url)));
    }
  }

  const hasNextPrev = /<link[^>]+rel=["'](?:next|prev)["']/i.test(html);
  const isPaginated = /[?&](page|p)=([2-9]|\d{2,})/i.test(url)
    || /\/page\/\d+/i.test(url);
  if (isPaginated && !hasNextPrev) {
    const rule = rules.get("EC-004");
    if (rule) issues.push(makeIssue(rule, evidenceEcommercePagination(url)));
  }

  return issues;
}
