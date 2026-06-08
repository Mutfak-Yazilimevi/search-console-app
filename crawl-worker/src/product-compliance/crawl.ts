import { assertSafeFetchUrl } from "../fetcher/ssrf-guard.js";
import { fetchPageWithRedirects } from "../fetcher/redirect-analyzer.js";
import { discoverStartUrls, fetchRobotsTxt } from "../fetcher/robots-sitemap.js";
import { extractLinks, extractTitle } from "../rules/analyzer.js";
import { extractProductFromPage, isProductUrl, type ExtractedProduct } from "./product-extract.js";
import { measureImageUrl, resetImageCheckBudget } from "./image-size.js";

export interface ProductComplianceJobData {
  productComplianceRunEntityId: string;
  url: string;
  maxProducts: number;
}

export interface ProductRescanJobData {
  productComplianceRunEntityId: string;
  productItemEntityId: string;
  url: string;
}

const PRODUCT_WEBHOOK_URL =
  process.env.PRODUCT_COMPLIANCE_WEBHOOK_URL
  ?? "http://localhost:5000/api/v1/public/merchant-center/compliance/webhook";

const PRODUCT_URL_PATTERN = /\/(product|products|urun|urunler|p|item|shop)\//i;

async function postWebhook(body: Record<string, unknown>, secret: string): Promise<void> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (secret) headers["X-Audit-Webhook-Secret"] = secret;

  const response = await fetch(PRODUCT_WEBHOOK_URL, {
    method: "POST",
    headers,
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Product compliance webhook failed: ${response.status} ${text}`);
  }
}

export async function runProductComplianceCrawl(
  job: ProductComplianceJobData,
  auditWebhookSecret: string,
  fetchDelayMs: number,
  isCancelled?: () => Promise<boolean>,
): Promise<void> {
  const startUrl = job.url.endsWith("/") ? job.url : `${job.url}/`;
  await assertSafeFetchUrl(startUrl);

  const origin = new URL(startUrl).origin;
  const robots = await fetchRobotsTxt(origin);
  const discoveredUrls = await discoverStartUrls(origin, robots, job.maxProducts * 5);
  const queue: string[] = [];
  const seen = new Set<string>();

  const enqueue = (u: string, priority = false) => {
    const normalized = u.split("#")[0]!;
    if (seen.has(normalized)) return;
    seen.add(normalized);
    if (priority) queue.unshift(normalized);
    else queue.push(normalized);
  };

  for (const u of discoveredUrls) {
    if (PRODUCT_URL_PATTERN.test(u) || isProductUrl(u)) enqueue(u, true);
  }

  enqueue(startUrl);
  const host = new URL(startUrl).host;

  resetImageCheckBudget();
  const products: ExtractedProduct[] = [];
  let siteHtmlParts: string[] = [];
  let pagesFetched = 0;
  const maxPages = Math.max(job.maxProducts * 3, 50);

  while (queue.length > 0 && products.length < job.maxProducts && pagesFetched < maxPages) {
    if (isCancelled && await isCancelled()) return;

    const pageUrl = queue.shift()!;
    pagesFetched++;

    try {
      await assertSafeFetchUrl(pageUrl);
      const { html, statusCode } = await fetchPageWithRedirects(pageUrl, origin);
      if (statusCode >= 400) continue;

      if (pagesFetched <= 3) siteHtmlParts.push(html.slice(0, 50_000));

      const product = extractProductFromPage(pageUrl, html);
      if (product) {
        if (product.image) {
          const dims = await measureImageUrl(product.image);
          if (dims) {
            product.mainImageWidth = dims.width;
            product.mainImageHeight = dims.height;
          }
        }
        products.push(product);
        await postWebhook({
          productComplianceRunEntityId: job.productComplianceRunEntityId,
          event: "product",
          url: pageUrl,
          title: product.name ?? extractTitle(html),
          extractedProductJson: JSON.stringify(product),
          progressPhase: "crawling",
          progressMessage: `${products.length} ürün bulundu`,
        }, auditWebhookSecret);
      }

      if (products.length < job.maxProducts) {
        const links = extractLinks(html, origin, pageUrl);
        for (const link of links) {
          try {
            const linkUri = new URL(link);
            if (linkUri.host !== host) continue;
            const pri = PRODUCT_URL_PATTERN.test(link) || isProductUrl(link);
            enqueue(link, pri);
          } catch {
            // skip invalid
          }
        }
      }
    } catch {
      // skip failed page
    }

    if (fetchDelayMs > 0) await new Promise((r) => setTimeout(r, fetchDelayMs));
  }

  if (isCancelled && await isCancelled()) return;

  await postWebhook({
    productComplianceRunEntityId: job.productComplianceRunEntityId,
    event: "complete",
    totalProducts: products.length,
    siteCheckHtml: siteHtmlParts.join("\n").slice(0, 150_000),
    progressPhase: "analyzing",
    progressMessage: "Analiz başlıyor",
  }, auditWebhookSecret);
}

export async function runSingleProductRescan(
  job: ProductRescanJobData,
  auditWebhookSecret: string,
): Promise<void> {
  const pageUrl = job.url.split("#")[0]!;
  await assertSafeFetchUrl(pageUrl);

  const origin = new URL(pageUrl).origin;
  resetImageCheckBudget();

  try {
    const { html, statusCode } = await fetchPageWithRedirects(pageUrl, origin);
    if (statusCode >= 400) {
      throw new Error(`HTTP ${statusCode} for ${pageUrl}`);
    }

    const product = extractProductFromPage(pageUrl, html);
    if (!product) {
      throw new Error(`Ürün verisi çıkarılamadı: ${pageUrl}`);
    }

    if (product.image) {
      const dims = await measureImageUrl(product.image);
      if (dims) {
        product.mainImageWidth = dims.width;
        product.mainImageHeight = dims.height;
      }
    }

    await postWebhook({
      productComplianceRunEntityId: job.productComplianceRunEntityId,
      productItemEntityId: job.productItemEntityId,
      event: "product-rescan",
      url: pageUrl,
      title: product.name ?? extractTitle(html),
      extractedProductJson: JSON.stringify(product),
      progressPhase: "rescanning",
      progressMessage: "Ürün yeniden analiz ediliyor",
    }, auditWebhookSecret);

    await postWebhook({
      productComplianceRunEntityId: job.productComplianceRunEntityId,
      productItemEntityId: job.productItemEntityId,
      event: "rescan-complete",
      progressPhase: "completed",
      progressMessage: "Ürün yeniden tarama tamamlandı",
    }, auditWebhookSecret);
  } catch (err) {
    const message = err instanceof Error ? err.message : "Ürün yeniden tarama başarısız.";
    await postWebhook({
      productComplianceRunEntityId: job.productComplianceRunEntityId,
      productItemEntityId: job.productItemEntityId,
      event: "rescan-failed",
      errorMessage: message,
    }, auditWebhookSecret);
  }
}

export async function failProductComplianceCrawl(
  entityId: string,
  message: string,
  auditWebhookSecret: string,
): Promise<void> {
  await postWebhook({
    productComplianceRunEntityId: entityId,
    event: "failed",
    errorMessage: message,
  }, auditWebhookSecret);
}
