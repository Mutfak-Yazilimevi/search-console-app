import type { CrawlIssue } from "./issue-factory.js";
import type { SeoRule } from "./rule-loader.js";
import type { PageAnalyzeOptions } from "./analyzer.js";

export type PageHeaders = PageAnalyzeOptions;

/** Sayfa bağlamı — kural motoru girdisi. */
export interface PageContext {
  url: string;
  statusCode: number;
  html: string;
  headers: PageHeaders;
}

/** Site bağlamı — site-level kurallar için. */
export interface SiteContext {
  origin: string;
}

/**
 * Google Search Central tabanlı SEO kural sözleşmesi.
 * Her kural JSON kataloğundan yüklenir ve `check` ile uygulanır.
 */
export interface ISeoRule extends SeoRule {
  check(page: PageContext, site: SiteContext): CrawlIssue[];
}
