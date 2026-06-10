import { createServer } from "node:http";
import { Queue, Worker, type Job } from "bullmq";
import { Redis } from "ioredis";
import { fetchRobotsTxt, isDisallowed, discoverStartUrls } from "./fetcher/robots-sitemap.js";
import {
  findBlockedCssJsRules,
  parseRobotsSyntaxWarnings,
  testGooglebotAccess,
} from "./fetcher/robots-analysis.js";
import { validatePrimarySitemap } from "./fetcher/sitemap-validator.js";
import {
  checkCanonicalTarget,
  fetchPageWithRedirects,
  hasTemporaryRedirect,
} from "./fetcher/redirect-analyzer.js";
import {
  normalizeCrawlUrl,
  scheduleCrawlUrl,
  shouldSkipQueuedEntry,
  urlPathDepth,
} from "./fetcher/crawl-depth.js";
import { assertSafeFetchUrl } from "./fetcher/ssrf-guard.js";
import {
  analyzePage,
  extractCanonical,
  extractLinks,
  extractMetaDescription,
  extractTitle,
  loadAllRules,
  makeIssue,
  severityToEnum,
  type CrawlIssue,
  type SeoRule,
} from "./rules/analyzer.js";
import { checkDoorwayTitles } from "./rules/checks/spam.check.js";
import {
  checkHreflangGraph,
  detectCctldHint,
  type HreflangEntry,
} from "./rules/checks/hreflang.check.js";
import { checkAmpHtmlLink } from "./rules/checks/amp.check.js";
import { extractAmpHtmlUrl, extractHreflangLinks } from "./rules/html-extractors.js";
import {
  evidenceCanonicalTargetError,
  evidenceDuplicateDescription,
  evidenceDuplicateTitle,
  evidenceGooglebotBlocked,
  evidenceHttpStatus,
  evidenceOrphanPage,
  evidenceRedirectChain,
  evidenceRedirectTemporary,
  evidenceRobotsBlocksAll,
  evidenceRobotsBlocksCssJs,
  evidenceRobotsSyntax,
  evidenceRobotsTxtMissing,
  evidenceSitemapGap,
  evidenceSitemapInvalid,
  evidenceSitemapLastmod,
  evidenceSitemapMissing,
  evidenceSitemapUnreachable,
} from "./rules/page-evidence.js";
import {
  diffJsSeo,
  needsJsRender,
  renderPage,
  resetRenderBudget,
  shutdownBrowser,
} from "./playwright/render.js";
import {
  runProductComplianceCrawl,
  failProductComplianceCrawl,
  runSingleProductRescan,
  type ProductComplianceJobData,
  type ProductRescanJobData,
} from "./product-compliance/crawl.js";
import {
  runPriceBenchmarkCrawl,
  failPriceBenchmarkCrawl,
  type PriceBenchmarkJobData,
} from "./price-benchmark/crawl.js";

interface CrawlJobData {
  auditRunEntityId: string;
  url: string;
  maxPages: number;
  maxDepth: number;
}

interface PageRecord {
  url: string;
  title: string | null;
  description: string | null;
  /** Geçici yönlendirme (302/303/307) — dup-title/dup-description adaylarından hariç */
  hasTemporaryRedirect: boolean;
}

function isDuplicateContentCandidate(page: PageRecord): boolean {
  return !page.hasTemporaryRedirect;
}

const redisUrl = process.env.REDIS_URL ?? "redis://localhost:6379";
const apiWebhookUrl =
  process.env.API_WEBHOOK_URL ?? "http://localhost:5000/api/v1/public/audit/webhook";
const auditWebhookSecret = process.env.AUDIT_WEBHOOK_SECRET ?? "";
const httpPort = Number(process.env.PORT ?? "3100");
const queueName = "audit-crawl";
const productComplianceQueueName = "product-compliance-crawl";
const priceBenchmarkQueueName = "price-benchmark-crawl";
const serpApiKey = process.env.SERP_API_KEY ?? "";
const fetchDelayMs = Number(process.env.FETCH_DELAY_MS ?? "500");
const concurrency = Number(process.env.CRAWL_CONCURRENCY ?? "3");

const connection = { url: redisUrl, maxRetriesPerRequest: null as null };
const queue = new Queue<CrawlJobData>(queueName, { connection });
const productComplianceQueue = new Queue<ProductComplianceJobData | ProductRescanJobData>(
  productComplianceQueueName,
  { connection },
);
const priceBenchmarkQueue = new Queue<PriceBenchmarkJobData>(priceBenchmarkQueueName, { connection });
const cancelRedis = new Redis(redisUrl, { maxRetriesPerRequest: null });
const cancelKeyPrefix = "audit:cancel:";
const productComplianceCancelKeyPrefix = "product-compliance:cancel:";
const priceBenchmarkCancelKeyPrefix = "price-benchmark:cancel:";
const rules = loadAllRules();

async function markCancelled(auditRunEntityId: string): Promise<void> {
  await cancelRedis.set(`${cancelKeyPrefix}${auditRunEntityId}`, "1", "EX", 86_400);
}

async function isCancelled(auditRunEntityId: string): Promise<boolean> {
  return (await cancelRedis.get(`${cancelKeyPrefix}${auditRunEntityId}`)) === "1";
}

async function markProductComplianceCancelled(entityId: string): Promise<void> {
  await cancelRedis.set(`${productComplianceCancelKeyPrefix}${entityId}`, "1", "EX", 86_400);
}

async function isProductComplianceCancelled(entityId: string): Promise<boolean> {
  return (await cancelRedis.get(`${productComplianceCancelKeyPrefix}${entityId}`)) === "1";
}

async function markPriceBenchmarkCancelled(entityId: string): Promise<void> {
  await cancelRedis.set(`${priceBenchmarkCancelKeyPrefix}${entityId}`, "1", "EX", 86_400);
}

async function isPriceBenchmarkCancelled(entityId: string): Promise<boolean> {
  return (await cancelRedis.get(`${priceBenchmarkCancelKeyPrefix}${entityId}`)) === "1";
}

async function removeQueuedPriceBenchmarkJobs(entityId: string): Promise<number> {
  let removed = 0;
  for (const state of ["waiting", "delayed", "paused"] as const) {
    const jobs = await priceBenchmarkQueue.getJobs([state]);
    for (const job of jobs) {
      if (job.data.priceBenchmarkRunEntityId === entityId) {
        await job.remove();
        removed++;
      }
    }
  }
  return removed;
}

async function removeQueuedJobs(auditRunEntityId: string): Promise<number> {
  let removed = 0;
  for (const state of ["waiting", "delayed", "paused"] as const) {
    const jobs = await queue.getJobs([state]);
    for (const job of jobs) {
      if (job.data.auditRunEntityId === auditRunEntityId) {
        await job.remove();
        removed++;
      }
    }
  }
  return removed;
}

async function removeQueuedProductComplianceJobs(entityId: string): Promise<number> {
  let removed = 0;
  for (const state of ["waiting", "delayed", "paused"] as const) {
    const jobs = await productComplianceQueue.getJobs([state]);
    for (const job of jobs) {
      const data = job.data as ProductComplianceJobData & ProductRescanJobData;
      if (data.productComplianceRunEntityId === entityId) {
        await job.remove();
        removed++;
      }
    }
  }
  return removed;
}

async function postWebhook(body: Record<string, unknown>): Promise<void> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (auditWebhookSecret) headers["X-Audit-Webhook-Secret"] = auditWebhookSecret;

  const response = await fetch(apiWebhookUrl, {
    method: "POST",
    headers,
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Webhook failed: ${response.status} ${text}`);
  }
}

function analyzeSiteLevel(
  pages: PageRecord[],
  linkedTargets: Set<string>,
  origin: string,
  sitemapUrls: Set<string>,
  crawledUrls: Set<string>,
  hreflangEntries: HreflangEntry[],
): CrawlIssue[] {
  const issues: CrawlIssue[] = [];

  const titleGroups = new Map<string, string[]>();
  for (const p of pages) {
    if (!p.title || !isDuplicateContentCandidate(p)) continue;
    const key = p.title.toLowerCase();
    const list = titleGroups.get(key) ?? [];
    list.push(p.url);
    titleGroups.set(key, list);
  }
  for (const [title, urls] of titleGroups) {
    if (urls.length > 1) {
      const rule = rules.get("dup-title");
      if (rule) issues.push(makeIssue(rule, evidenceDuplicateTitle(title, urls.slice(0, 10))));
    }
  }

  const descGroups = new Map<string, string[]>();
  for (const p of pages) {
    if (!p.description || !isDuplicateContentCandidate(p)) continue;
    const key = p.description.toLowerCase();
    const list = descGroups.get(key) ?? [];
    list.push(p.url);
    descGroups.set(key, list);
  }
  for (const [desc, urls] of descGroups) {
    if (urls.length > 1) {
      const rule = rules.get("dup-description");
      if (rule) issues.push(makeIssue(rule, evidenceDuplicateDescription(desc, urls.slice(0, 10))));
    }
  }

  const originNorm = origin.replace(/\/$/, "");
  for (const p of pages) {
    const norm = p.url.replace(/\/$/, "") || p.url;
    if (norm === originNorm) continue;
    if (!linkedTargets.has(norm)) {
      const rule = rules.get("orphan-page");
      if (rule) issues.push(makeIssue(rule, evidenceOrphanPage(norm)));
    }
  }

  if (sitemapUrls.size > 5) {
    let missing = 0;
    const missingSamples: string[] = [];
    for (const su of sitemapUrls) {
      const n = su.replace(/\/$/, "") || su;
      if (!crawledUrls.has(n)) {
        missing++;
        if (missingSamples.length < 10) missingSamples.push(su);
      }
    }
    const ratio = missing / sitemapUrls.size;
    if (ratio > 0.3) {
      const rule = rules.get("sitemap-coverage-gap");
      if (rule) {
        issues.push(makeIssue(rule, evidenceSitemapGap(missing, sitemapUrls.size, missingSamples)));
      }
    }
  }

  issues.push(...checkDoorwayTitles(pages, rules));
  issues.push(...checkHreflangGraph(hreflangEntries, crawledUrls, rules));
  issues.push(...detectCctldHint(origin, hreflangEntries, rules));

  return issues;
}

async function crawlSite(job: CrawlJobData): Promise<void> {
  resetRenderBudget();
  const { auditRunEntityId, url: startUrl, maxPages, maxDepth } = job;
  if (await isCancelled(auditRunEntityId)) {
    console.log(`Crawl skipped (cancelled): ${auditRunEntityId}`);
    return;
  }
  assertSafeFetchUrl(startUrl);
  const origin = new URL(startUrl).origin;

  const robots = await fetchRobotsTxt(origin);
  if (robots.disallowed.some((d) => d === "/")) {
    const rule = rules.get("robots-blocks-all");
    if (rule) {
      await postWebhook({
        auditRunEntityId,
        event: "page",
        url: origin,
        statusCode: 200,
        title: null,
        metaDescription: null,
        crawlDepth: 0,
        responseTimeMs: 0,
        issues: [makeWebhookIssue(rule, evidenceRobotsBlocksAll())],
      });
    }
  }

  const seedUrls = await discoverStartUrls(origin, robots, maxPages);
  const sitemapUrlSet = new Set(seedUrls.map((u) => u.replace(/\/$/, "") || u));

  if (robots.disallowed.length === 0) {
    try {
      const robotsRes = await fetch(`${origin}/robots.txt`);
      if (!robotsRes.ok) {
        const rule = rules.get("robots-txt-missing");
        if (rule) {
          await postWebhook({
            auditRunEntityId,
            event: "page",
            url: origin,
            statusCode: 200,
            title: null,
            metaDescription: null,
            crawlDepth: 0,
            responseTimeMs: 0,
            issues: [makeWebhookIssue(rule, evidenceRobotsTxtMissing(origin))],
          });
        }
      }
    } catch { /* ignore */ }
  }

  if (robots.sitemaps.length === 0 && seedUrls.length <= 1) {
    const rule = rules.get("sitemap-missing");
    if (rule) {
      await postWebhook({
        auditRunEntityId,
        event: "page",
        url: origin,
        statusCode: 200,
        title: null,
        metaDescription: null,
        crawlDepth: 0,
        responseTimeMs: 0,
        issues: [makeWebhookIssue(rule, evidenceSitemapMissing(origin))],
      });
    }
  }

  const originIssues: CrawlIssue[] = [];

  const cssJsBlocked = findBlockedCssJsRules(robots);
  if (cssJsBlocked.length > 0) {
    const rule = rules.get("robots-blocks-css-js");
    if (rule) originIssues.push(makeIssue(rule, evidenceRobotsBlocksCssJs(cssJsBlocked)));
  }

  const syntaxWarnings = parseRobotsSyntaxWarnings(robots.rawText);
  if (syntaxWarnings.length > 0) {
    const rule = rules.get("robots-syntax-warning");
    if (rule) originIssues.push(makeIssue(rule, evidenceRobotsSyntax(syntaxWarnings)));
  }

  const sitemapValidation = await validatePrimarySitemap(origin, robots);
  if (!sitemapValidation.xmlValid) {
    const rule = rules.get("sitemap-xml-invalid");
    if (rule) originIssues.push(makeIssue(rule, evidenceSitemapInvalid(sitemapValidation.detail)));
  } else {
    if (sitemapValidation.invalidLastModCount > 0) {
      const rule = rules.get("sitemap-lastmod-invalid");
      if (rule) {
        originIssues.push(makeIssue(rule, evidenceSitemapLastmod(sitemapValidation.invalidLastModCount)));
      }
    }
    if (sitemapValidation.unreachableLocCount > 0 && sitemapValidation.sampledLocs > 0) {
      const rule = rules.get("sitemap-loc-unreachable");
      if (rule) {
        originIssues.push(makeIssue(
          rule,
          evidenceSitemapUnreachable(
            sitemapValidation.unreachableLocCount,
            sitemapValidation.sampledLocs,
          ),
        ));
      }
    }
  }

  if (originIssues.length > 0) {
    await postWebhook({
      auditRunEntityId,
      event: "page",
      url: origin,
      statusCode: 200,
      title: null,
      metaDescription: null,
      crawlDepth: 0,
      responseTimeMs: 0,
      issues: originIssues,
    });
  }

  const urlQueue: { url: string; depth: number }[] = [];
  const queuedDepth = new Map<string, number>();
  const crawled = new Set<string>();
  const startNorm = normalizeCrawlUrl(startUrl);

  scheduleCrawlUrl(urlQueue, queuedDepth, crawled, startNorm, 0);
  const sitemapSeeds = seedUrls
    .map((u) => normalizeCrawlUrl(u))
    .filter((u) => u !== startNorm)
    .sort((a, b) => urlPathDepth(a) - urlPathDepth(b));

  for (const seed of sitemapSeeds) {
    scheduleCrawlUrl(urlQueue, queuedDepth, crawled, seed, urlPathDepth(seed));
  }
  const linkedTargets = new Set<string>();
  const linkTargetCounts = new Map<string, number>();
  let totalInternalLinks = 0;
  const pageRecords: PageRecord[] = [];
  const hreflangEntries: HreflangEntry[] = [];
  let pagesCrawled = 0;

  linkedTargets.add(origin.replace(/\/$/, ""));

  while (urlQueue.length > 0 && pagesCrawled < maxPages) {
    if (await isCancelled(auditRunEntityId)) {
      console.log(`Crawl stopped (cancelled): ${auditRunEntityId}`);
      return;
    }

    const { url, depth } = urlQueue.shift()!;
    const normalized = normalizeCrawlUrl(url);
    if (shouldSkipQueuedEntry(normalized, depth, queuedDepth, crawled)) continue;
    if (isDisallowed(normalized, robots)) continue;

    const crawlDepth = queuedDepth.get(normalized) ?? depth;
    crawled.add(normalized);
    pagesCrawled++;

    let issues: CrawlIssue[] = [];
    let statusCode = 0;
    let title: string | null = null;
    let metaDescription: string | null = null;
    let responseTimeMs = 0;
    let html = "";

    try {
      const result = await fetchPageWithRedirects(normalized, origin);
      statusCode = result.statusCode;
      html = result.html;
      responseTimeMs = result.responseTimeMs;
      title = extractTitle(html);
      metaDescription = extractMetaDescription(html);

      issues = analyzePage(normalized, statusCode, html, rules, {
        xRobotsTag: result.headers["x-robots-tag"] ?? null,
        isShallowPage: crawlDepth <= 1,
      });

      if (result.redirectHops >= 3) {
        const rule = rules.get("redirect-chain-long");
        if (rule) {
          issues.push(makeIssue(rule, evidenceRedirectChain(result.redirectHops, result.redirectStatuses)));
        }
      }

      if (hasTemporaryRedirect(result.redirectStatuses)) {
        const rule = rules.get("redirect-temporary-302");
        if (rule) {
          issues.push(makeIssue(rule, evidenceRedirectTemporary(result.redirectStatuses)));
        }
      }

      const canonical = extractCanonical(html);
      if (canonical) {
        const canonicalStatus = await checkCanonicalTarget(canonical, origin);
        if (canonicalStatus >= 400 || canonicalStatus === 0) {
          const rule = rules.get("canonical-target-error");
          if (rule) issues.push(makeIssue(rule, evidenceCanonicalTargetError(canonical, canonicalStatus)));
        }
      }

      if (crawlDepth === 0) {
        const gb = await testGooglebotAccess(normalized, statusCode);
        if (gb.blocked) {
          const rule = rules.get("googlebot-blocked");
          if (rule) {
            issues.push(makeIssue(rule, evidenceGooglebotBlocked(gb.googlebotStatus)));
          }
        }

        const ampUrl = extractAmpHtmlUrl(html);
        if (ampUrl) {
          issues.push(...await checkAmpHtmlLink(ampUrl, normalized, origin, rules));
        }
      }

      for (const hl of extractHreflangLinks(html)) {
        try {
          const resolved = new URL(hl.href, normalized).href.replace(/\/$/, "") || new URL(hl.href, normalized).href;
          hreflangEntries.push({ pageUrl: normalized, lang: hl.lang, targetUrl: resolved });
        } catch { /* skip invalid */ }
      }

      if (needsJsRender(html)) {
        try {
          const rendered = await renderPage(normalized);
          issues.push(...diffJsSeo(html, rendered, rules, statusCode));
          if (!title) title = extractTitle(rendered);
          if (!metaDescription) metaDescription = extractMetaDescription(rendered);
        } catch (err) {
          console.warn(`Playwright render failed for ${normalized}:`, err);
        }
      }

      pageRecords.push({
        url: normalized,
        title,
        description: metaDescription,
        hasTemporaryRedirect: hasTemporaryRedirect(result.redirectStatuses),
      });

      if (crawlDepth < maxDepth && html) {
        const links = extractLinks(html, origin, normalized);
        for (const link of links) {
          const linkNorm = normalizeCrawlUrl(link);
          linkedTargets.add(linkNorm);
          totalInternalLinks++;
          linkTargetCounts.set(linkNorm, (linkTargetCounts.get(linkNorm) ?? 0) + 1);
          scheduleCrawlUrl(urlQueue, queuedDepth, crawled, linkNorm, crawlDepth + 1);
        }
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "Sayfa getirilemedi";
      console.warn(`Page fetch failed for ${normalized}:`, err);
      statusCode = 0;
      const rule = rules.get("http-status-error");
      if (rule) issues.push(makeIssue(rule, evidenceHttpStatus(0)));
      await postWebhook({
        auditRunEntityId,
        event: "page",
        url: normalized,
        statusCode,
        title,
        metaDescription,
        crawlDepth,
        responseTimeMs,
        issues,
        progressPhase: "crawling",
        progressMessage: `${pagesCrawled} sayfa tarandı (${message})`,
      });
      if (fetchDelayMs > 0) await new Promise((r) => setTimeout(r, fetchDelayMs));
      continue;
    }

    await postWebhook({
      auditRunEntityId,
      event: "page",
      url: normalized,
      statusCode,
      title,
      metaDescription,
      crawlDepth,
      responseTimeMs,
      issues,
      progressPhase: "crawling",
      progressMessage: `${pagesCrawled} sayfa tarandı`,
    });

    if (fetchDelayMs > 0) await new Promise((r) => setTimeout(r, fetchDelayMs));
  }

  if (await isCancelled(auditRunEntityId)) {
    console.log(`Crawl complete skipped (cancelled): ${auditRunEntityId}`);
    return;
  }

  const siteIssues = analyzeSiteLevel(pageRecords, linkedTargets, origin, sitemapUrlSet, crawled, hreflangEntries);
  if (siteIssues.length > 0) {
    await postWebhook({
      auditRunEntityId,
      event: "page",
      url: origin,
      statusCode: 200,
      title: null,
      metaDescription: null,
      crawlDepth: 0,
      responseTimeMs: 0,
      issues: siteIssues,
    });
  }

  const topLinked = [...linkTargetCounts.entries()]
    .sort((a, b) => b[1] - a[1])
    .slice(0, 10)
    .map(([url, count]) => ({ url, count }));

  await postWebhook({
    auditRunEntityId,
    event: "complete",
    totalPages: pagesCrawled,
    internalLinkCount: totalInternalLinks,
    topLinkedPagesJson: JSON.stringify(topLinked),
  });
}

function makeWebhookIssue(
  rule: { id: string; category: string; message: string; fixHint?: string; docUrl?: string; severity: string },
  evidence?: string,
) {
  const sev = rule.severity as SeoRule["severity"];
  return {
    ruleId: rule.id,
    category: rule.category,
    severity: severityToEnum(sev === "critical" || sev === "warning" || sev === "info" ? sev : "info"),
    message: rule.message,
    fixHint: rule.fixHint,
    docUrl: rule.docUrl,
    evidence,
  };
}

async function processJob(job: Job<CrawlJobData>): Promise<void> {
  console.log(`Processing crawl job ${job.id} for ${job.data.url} (max ${job.data.maxPages} pages)`);
  try {
    await crawlSite(job.data);
    console.log(`Completed crawl job ${job.id}`);
  } finally {
    await shutdownBrowser();
  }
}

const worker = new Worker<CrawlJobData>(
  queueName,
  processJob,
  { connection, concurrency },
);

worker.on("failed", async (job, err) => {
  console.error(`Job ${job?.id} failed:`, err);
  if (job?.data.auditRunEntityId) {
    try {
      await postWebhook({
        auditRunEntityId: job.data.auditRunEntityId,
        event: "failed",
        errorMessage: err.message,
      });
    } catch { /* ignore */ }
  }
  await shutdownBrowser();
});

const productComplianceWorker = new Worker<ProductComplianceJobData | ProductRescanJobData>(
  productComplianceQueueName,
  async (job) => {
    const data = job.data as ProductComplianceJobData & ProductRescanJobData;
    if ("productItemEntityId" in data && data.productItemEntityId) {
      console.log(`Processing product rescan job ${job.id} for ${data.url}`);
      await runSingleProductRescan(data, auditWebhookSecret);
      return;
    }
    console.log(`Processing product compliance job ${job.id} for ${data.url}`);
    await runProductComplianceCrawl(
      data as ProductComplianceJobData,
      auditWebhookSecret,
      fetchDelayMs,
      () => isProductComplianceCancelled(data.productComplianceRunEntityId),
    );
  },
  { connection, concurrency: 1 },
);

productComplianceWorker.on("failed", async (job, err) => {
  console.error(`Product compliance job ${job?.id} failed:`, err);
  if (job?.data.productComplianceRunEntityId) {
    try {
      await failProductComplianceCrawl(
        job.data.productComplianceRunEntityId,
        err.message,
        auditWebhookSecret,
      );
    } catch { /* ignore */ }
  }
});

const priceBenchmarkWorker = new Worker<PriceBenchmarkJobData>(
  priceBenchmarkQueueName,
  async (job) => {
    console.log(`Processing price benchmark job ${job.id} for ${job.data.url}`);
    await runPriceBenchmarkCrawl(
      job.data,
      auditWebhookSecret,
      fetchDelayMs,
      serpApiKey,
      () => isPriceBenchmarkCancelled(job.data.priceBenchmarkRunEntityId),
    );
  },
  { connection, concurrency: 1 },
);

priceBenchmarkWorker.on("failed", async (job, err) => {
  console.error(`Price benchmark job ${job?.id} failed:`, err);
  if (job?.data.priceBenchmarkRunEntityId) {
    try {
      await failPriceBenchmarkCrawl(
        job.data.priceBenchmarkRunEntityId,
        err.message,
        auditWebhookSecret,
      );
    } catch { /* ignore */ }
  }
});

createServer(async (req, res) => {
  if (req.method === "POST" && req.url === "/cancel") {
    try {
      const chunks: Buffer[] = [];
      for await (const chunk of req) chunks.push(chunk as Buffer);
      const body = JSON.parse(Buffer.concat(chunks).toString("utf8")) as {
        auditRunEntityId?: string;
        productComplianceRunEntityId?: string;
        priceBenchmarkRunEntityId?: string;
      };

      if (!body.auditRunEntityId && !body.productComplianceRunEntityId && !body.priceBenchmarkRunEntityId) {
        res.writeHead(400, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ error: "auditRunEntityId, productComplianceRunEntityId or priceBenchmarkRunEntityId is required" }));
        return;
      }

      let removed = 0;
      if (body.auditRunEntityId) {
        await markCancelled(body.auditRunEntityId);
        removed += await removeQueuedJobs(body.auditRunEntityId);
      }
      if (body.productComplianceRunEntityId) {
        await markProductComplianceCancelled(body.productComplianceRunEntityId);
        removed += await removeQueuedProductComplianceJobs(body.productComplianceRunEntityId);
      }
      if (body.priceBenchmarkRunEntityId) {
        await markPriceBenchmarkCancelled(body.priceBenchmarkRunEntityId);
        removed += await removeQueuedPriceBenchmarkJobs(body.priceBenchmarkRunEntityId);
      }

      res.writeHead(200, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ ok: true, removedJobs: removed }));
    } catch (err) {
      res.writeHead(500, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ error: err instanceof Error ? err.message : "cancel failed" }));
    }
    return;
  }

  if (req.method === "POST" && req.url === "/enqueue") {
    try {
      const chunks: Buffer[] = [];
      for await (const chunk of req) chunks.push(chunk as Buffer);
      const body = JSON.parse(Buffer.concat(chunks).toString("utf8")) as CrawlJobData;

      if (!body.auditRunEntityId || !body.url) {
        res.writeHead(400, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ error: "auditRunEntityId and url are required" }));
        return;
      }

      body.maxPages = body.maxPages ?? 50;
      body.maxDepth = body.maxDepth ?? 5;

      const bullJob = await queue.add("site-crawl", body, {
        removeOnComplete: 100,
        removeOnFail: 50,
      });

      res.writeHead(202, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ ok: true, jobId: bullJob.id }));
    } catch (err) {
      res.writeHead(500, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ error: err instanceof Error ? err.message : "enqueue failed" }));
    }
    return;
  }

  if (req.method === "POST" && req.url === "/enqueue-product-compliance") {
    try {
      const chunks: Buffer[] = [];
      for await (const chunk of req) chunks.push(chunk as Buffer);
      const body = JSON.parse(Buffer.concat(chunks).toString("utf8")) as ProductComplianceJobData;

      if (!body.productComplianceRunEntityId || !body.url) {
        res.writeHead(400, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ error: "productComplianceRunEntityId and url are required" }));
        return;
      }

      body.maxProducts = body.maxProducts ?? 100;

      const bullJob = await productComplianceQueue.add("product-compliance", body, {
        removeOnComplete: 100,
        removeOnFail: 50,
      });

      res.writeHead(202, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ ok: true, jobId: bullJob.id }));
    } catch (err) {
      res.writeHead(500, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ error: err instanceof Error ? err.message : "enqueue failed" }));
    }
    return;
  }

  if (req.method === "POST" && req.url === "/enqueue-product-rescan") {
    try {
      const chunks: Buffer[] = [];
      for await (const chunk of req) chunks.push(chunk as Buffer);
      const body = JSON.parse(Buffer.concat(chunks).toString("utf8")) as ProductRescanJobData;

      if (!body.productComplianceRunEntityId || !body.productItemEntityId || !body.url) {
        res.writeHead(400, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ error: "productComplianceRunEntityId, productItemEntityId and url are required" }));
        return;
      }

      const bullJob = await productComplianceQueue.add("product-rescan", body, {
        removeOnComplete: 100,
        removeOnFail: 50,
      });

      res.writeHead(202, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ ok: true, jobId: bullJob.id }));
    } catch (err) {
      res.writeHead(500, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ error: err instanceof Error ? err.message : "enqueue failed" }));
    }
    return;
  }

  if (req.method === "POST" && req.url === "/enqueue-price-benchmark") {
    try {
      const chunks: Buffer[] = [];
      for await (const chunk of req) chunks.push(chunk as Buffer);
      const body = JSON.parse(Buffer.concat(chunks).toString("utf8")) as PriceBenchmarkJobData;

      if (!body.priceBenchmarkRunEntityId || !body.url) {
        res.writeHead(400, { "Content-Type": "application/json" });
        res.end(JSON.stringify({ error: "priceBenchmarkRunEntityId and url are required" }));
        return;
      }

      body.maxProducts = body.maxProducts ?? 100;

      const bullJob = await priceBenchmarkQueue.add("price-benchmark", body, {
        removeOnComplete: 100,
        removeOnFail: 50,
      });

      res.writeHead(202, { "Content-Type": "application/json" });
      res.end(JSON.stringify({
        ok: true,
        jobId: bullJob.id,
        shoppingBrowserEnabled: process.env.PLAYWRIGHT_ENABLED !== "false",
      }));
    } catch (err) {
      res.writeHead(500, { "Content-Type": "application/json" });
      res.end(JSON.stringify({ error: err instanceof Error ? err.message : "enqueue failed" }));
    }
    return;
  }

  if (req.method === "GET" && req.url === "/health") {
    res.writeHead(200, { "Content-Type": "application/json" });
    res.end(JSON.stringify({ status: "ok", rulesLoaded: rules.size, playwright: process.env.PLAYWRIGHT_ENABLED !== "false" }));
    return;
  }

  res.writeHead(404);
  res.end();
}).listen(httpPort, () => {
  console.log(`Crawl worker :${httpPort} | rules=${rules.size} | concurrency=${concurrency}`);
});
