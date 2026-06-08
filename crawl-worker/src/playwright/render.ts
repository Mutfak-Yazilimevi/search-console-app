import { chromium, type Browser } from "playwright";
import { extractTitle, countH1, type CrawlIssue, type SeoRule, makeIssue } from "../rules/analyzer.js";
import { extractBodyText } from "../rules/html-extractors.js";
import {
  evidenceJsCloakingScore,
  evidenceJsH1MissingStatic,
  evidenceJsLazyContent,
  evidenceJsSoft404,
  evidenceJsTitleChanged,
  evidenceJsTitleMissingStatic,
} from "../rules/page-evidence.js";

let browser: Browser | null = null;
const enabled = process.env.PLAYWRIGHT_ENABLED !== "false";
const maxRendersPerJob = Number(process.env.PLAYWRIGHT_MAX_PAGES ?? "20");
let renderCount = 0;

export async function getBrowser(): Promise<Browser> {
  if (!browser) {
    browser = await chromium.launch({
      headless: true,
      args: ["--disable-gpu", "--no-sandbox", "--disable-dev-shm-usage"],
    });
  }
  return browser;
}

export function resetRenderBudget(): void {
  renderCount = 0;
}

export function needsJsRender(html: string): boolean {
  if (!enabled || renderCount >= maxRendersPerJob) return false;

  const title = extractTitle(html);
  const textContent = html
    .replace(/<script[\s\S]*?<\/script>/gi, "")
    .replace(/<style[\s\S]*?<\/style>/gi, "")
    .replace(/<[^>]+>/g, " ")
    .replace(/\s+/g, " ")
    .trim();

  const isSpaShell =
    /<div[^>]+id=["'](root|app|__next|__nuxt)["']/i.test(html) ||
    /<app-root/i.test(html);

  return (
    !title ||
    countH1(html) === 0 ||
    (isSpaShell && textContent.length < 150)
  );
}

export async function renderPage(url: string): Promise<string> {
  renderCount++;
  const b = await getBrowser();
  const context = await b.newContext({
    userAgent: "SearchConsoleApp-CrawlWorker/1.0 (Playwright)",
  });
  const page = await context.newPage();
  try {
    await page.goto(url, { waitUntil: "domcontentloaded", timeout: 20000 });
    await page.waitForTimeout(1500);
    return await page.content();
  } finally {
    await context.close();
  }
}

export function diffJsSeo(
  staticHtml: string,
  renderedHtml: string,
  rules: Map<string, SeoRule>,
  statusCode = 200,
): CrawlIssue[] {
  const issues: CrawlIssue[] = [];
  const staticTitle = extractTitle(staticHtml);
  const renderedTitle = extractTitle(renderedHtml);
  const staticH1 = countH1(staticHtml);
  const renderedH1 = countH1(renderedHtml);
  const staticWords = extractBodyText(staticHtml).split(/\s+/).filter(Boolean).length;
  const renderedWords = extractBodyText(renderedHtml).split(/\s+/).filter(Boolean).length;

  if (!staticTitle && renderedTitle) {
    const rule = rules.get("js-title-missing-static");
    if (rule) issues.push(makeIssue(rule, evidenceJsTitleMissingStatic(renderedTitle)));
  } else if (staticTitle && renderedTitle && staticTitle !== renderedTitle) {
    const rule = rules.get("js-title-changed");
    if (rule) issues.push(makeIssue(rule, evidenceJsTitleChanged(staticTitle, renderedTitle)));
  }

  if (staticH1 === 0 && renderedH1 > 0) {
    const rule = rules.get("js-h1-missing-static");
    if (rule) issues.push(makeIssue(rule, evidenceJsH1MissingStatic(renderedH1)));
  }

  if (statusCode === 200) {
    const notFoundPattern = /404|not found|sayfa bulunamad|bulunamadı|page not found/i;
    const staticTitleNF = staticTitle && notFoundPattern.test(staticTitle);
    const renderedTitleNF = renderedTitle && notFoundPattern.test(renderedTitle);
    const staticBodyNF = notFoundPattern.test(extractBodyText(staticHtml).slice(0, 500));
    if ((staticTitleNF || staticBodyNF) && renderedTitle && !notFoundPattern.test(renderedTitle)) {
      const rule = rules.get("js-soft-404");
      if (rule) issues.push(makeIssue(rule, evidenceJsSoft404(staticTitle ?? "404 içerik", renderedTitle)));
    }
    if (!staticTitleNF && !staticBodyNF && renderedTitleNF) {
      const rule = rules.get("js-soft-404");
      if (rule) issues.push(makeIssue(rule, evidenceJsSoft404("Normal içerik", renderedTitle ?? "404 içerik")));
    }
  }

  if (staticWords > 0 && renderedWords > staticWords * 1.5) {
    const rule = rules.get("js-lazy-content");
    if (rule) {
      issues.push(makeIssue(rule, evidenceJsLazyContent(staticWords, renderedWords)));
    }
  }

  const titleDiff = staticTitle && renderedTitle && staticTitle !== renderedTitle;
  const h1Diff = staticH1 !== renderedH1;
  const wordRatio = staticWords > 50 && renderedWords > 0
    ? Math.abs(renderedWords - staticWords) / staticWords
    : 0;
  if ((titleDiff && h1Diff) || wordRatio > 0.6) {
    const rule = rules.get("js-cloaking-score");
    if (rule) {
      issues.push(makeIssue(rule, evidenceJsCloakingScore(!!titleDiff, h1Diff, Math.round(wordRatio * 100))));
    }
  }

  return issues;
}

export async function shutdownBrowser(): Promise<void> {
  if (browser) {
    await browser.close();
    browser = null;
  }
}
