import { assertSafeFetchUrl } from "../fetcher/ssrf-guard.js";
import { fetchPageWithRedirects } from "../fetcher/redirect-analyzer.js";
import { discoverStartUrls, fetchRobotsTxt } from "../fetcher/robots-sitemap.js";
import { extractLinks } from "../rules/analyzer.js";
import {
  extractProductFromPage,
  resolveOurProductPrice,
  type ExtractedProduct,
} from "../product-compliance/product-extract.js";
import { renderPage, needsJsRender } from "../playwright/render.js";
import { compareToMarket, type ShoppingPriceStats } from "./shopping-types.js";
import { isShoppingBrowserEnabled, searchGoogleShoppingBrowser } from "./shopping-playwright.js";
import { searchGoogleShoppingSerpApi } from "./shopping-serpapi.js";

export interface PriceBenchmarkJobData {
  priceBenchmarkRunEntityId: string;
  url: string;
  maxProducts: number;
}

interface DiscoveredProduct {
  pageUrl: string;
  product: ExtractedProduct;
  ourPrice: number | undefined;
}

const WEBHOOK_URL =
  process.env.PRICE_BENCHMARK_WEBHOOK_URL
  ?? "http://localhost:5000/api/v1/public/price-benchmark/webhook";

const SHOPPING_DELAY_MS = Number(process.env.SHOPPING_SEARCH_DELAY_MS ?? "2000");
const SHOPPING_USE_SERPAPI = process.env.SHOPPING_USE_SERPAPI === "true";

async function postWebhook(body: Record<string, unknown>, secret: string): Promise<void> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (secret) headers["X-Audit-Webhook-Secret"] = secret;

  const response = await fetch(WEBHOOK_URL, {
    method: "POST",
    headers,
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Price benchmark webhook failed: ${response.status} ${text}`);
  }
}

function isSkippablePath(url: string): boolean {
  const lower = url.toLowerCase();
  return /\/(cart|checkout|account|login|register|blog|haber|news|contact|iletisim|hakkimizda|about|faq|sss)\b/.test(lower);
}

async function extractProduct(pageUrl: string, html: string): Promise<ExtractedProduct | null> {
  let product = extractProductFromPage(pageUrl, html);
  if (!product?.name) return null;

  let ourPrice = resolveOurProductPrice(product);
  const suspiciousPrice = ourPrice === 500 && product.schemaPrice == null;
  const shouldRender = ourPrice == null || suspiciousPrice || needsJsRender(html);

  if (shouldRender && process.env.PLAYWRIGHT_ENABLED !== "false") {
    try {
      const rendered = await renderPage(pageUrl);
      const renderedProduct = extractProductFromPage(pageUrl, rendered);
      if (renderedProduct?.name) {
        product = renderedProduct;
        ourPrice = resolveOurProductPrice(product);
      }
    } catch {
      // keep static extraction
    }
  }

  return product;
}

async function searchMarketPrices(
  product: ExtractedProduct,
  serpApiKey: string,
): Promise<{ stats: ShoppingPriceStats; shoppingError?: string }> {
  const empty: ShoppingPriceStats = {
    offers: [],
    minPrice: null,
    maxPrice: null,
    weightedAvgPrice: null,
    minOffer: null,
    maxOffer: null,
    offerCount: 0,
  };

  let browserError: string | undefined;

  if (isShoppingBrowserEnabled()) {
    try {
      const stats = await searchGoogleShoppingBrowser(product.name!);
      if (stats.offerCount > 0) return { stats };
      browserError = "Google Shopping sonucu bulunamadı";
    } catch (err) {
      browserError = err instanceof Error ? err.message : "Tarayıcı Shopping araması başarısız";
    }
  } else {
    browserError = "Tarayıcı Shopping devre dışı (PLAYWRIGHT_ENABLED=false)";
  }

  const useSerpApi = serpApiKey && (SHOPPING_USE_SERPAPI || !!browserError);
  if (useSerpApi) {
    try {
      const stats = await searchGoogleShoppingSerpApi(product.name!, product.brand, serpApiKey);
      if (stats.offerCount > 0) return { stats };
      return {
        stats: empty,
        shoppingError: browserError ?? "SerpAPI sonucu bulunamadı",
      };
    } catch (err) {
      return {
        stats: empty,
        shoppingError: err instanceof Error ? err.message : "SerpAPI araması başarısız",
      };
    }
  }

  return { stats: empty, shoppingError: browserError ?? "Google Shopping sonucu bulunamadı" };
}

async function discoverProducts(
  job: PriceBenchmarkJobData,
  webhookSecret: string,
  fetchDelayMs: number,
  isCancelled?: () => Promise<boolean>,
): Promise<DiscoveredProduct[]> {
  const startUrl = job.url.endsWith("/") ? job.url : `${job.url}/`;
  await assertSafeFetchUrl(startUrl);

  const origin = new URL(startUrl).origin;
  const robots = await fetchRobotsTxt(origin);
  const discoveredUrls = await discoverStartUrls(origin, robots, job.maxProducts * 10);
  const queue: string[] = [];
  const seen = new Set<string>();

  const enqueue = (u: string, front = false) => {
    const normalized = u.split("#")[0]!;
    if (seen.has(normalized) || isSkippablePath(normalized)) return;
    seen.add(normalized);
    if (front) queue.unshift(normalized);
    else queue.push(normalized);
  };

  enqueue(startUrl, true);
  for (const u of discoveredUrls) enqueue(u);

  const host = new URL(startUrl).host;
  let pagesFetched = 0;
  const discovered: DiscoveredProduct[] = [];
  const maxPages = Math.max(job.maxProducts * 5, 100);

  while (queue.length > 0 && discovered.length < job.maxProducts && pagesFetched < maxPages) {
    if (isCancelled && await isCancelled()) return discovered;

    const pageUrl = queue.shift()!;
    pagesFetched++;

    try {
      await assertSafeFetchUrl(pageUrl);
      const { html, statusCode } = await fetchPageWithRedirects(pageUrl, origin);
      if (statusCode >= 400) continue;

      const product = await extractProduct(pageUrl, html);
      if (!product?.name) {
        if (html.includes("<a ") || html.includes("href=")) {
          for (const link of extractLinks(html, origin, pageUrl)) {
            try {
              if (new URL(link).host === host) enqueue(link);
            } catch { /* skip */ }
          }
        }
        continue;
      }

      const ourPrice = resolveOurProductPrice(product);
      discovered.push({ pageUrl, product, ourPrice });

      await postWebhook({
        priceBenchmarkRunEntityId: job.priceBenchmarkRunEntityId,
        event: "discovered",
        url: pageUrl,
        title: product.name,
        ourPrice,
        priceCurrency: product.priceCurrency ?? "TRY",
        extractedProductJson: JSON.stringify(product),
        progressPhase: "discovering",
        progressMessage: `${discovered.length} ürün bulundu`,
      }, webhookSecret);
    } catch {
      // skip failed page
    }

    if (fetchDelayMs > 0) await new Promise((r) => setTimeout(r, fetchDelayMs));
  }

  return discovered;
}

async function compareDiscoveredProducts(
  job: PriceBenchmarkJobData,
  products: DiscoveredProduct[],
  webhookSecret: string,
  serpApiKey: string,
  isCancelled?: () => Promise<boolean>,
): Promise<void> {
  for (let i = 0; i < products.length; i++) {
    if (isCancelled && await isCancelled()) return;

    const { pageUrl, product, ourPrice } = products[i]!;
    const { stats, shoppingError } = await searchMarketPrices(product, serpApiKey);
    const comparison = compareToMarket(ourPrice, stats.weightedAvgPrice);

    await postWebhook({
      priceBenchmarkRunEntityId: job.priceBenchmarkRunEntityId,
      event: "compared",
      url: pageUrl,
      title: product.name,
      ourPrice,
      priceCurrency: product.priceCurrency ?? "TRY",
      extractedProductJson: JSON.stringify(product),
      shoppingOffersJson: JSON.stringify(stats.offers),
      minMarketPrice: stats.minPrice,
      maxMarketPrice: stats.maxPrice,
      weightedAvgMarketPrice: stats.weightedAvgPrice,
      minOfferLink: stats.minOffer?.link,
      minOfferSource: stats.minOffer?.source,
      maxOfferLink: stats.maxOffer?.link,
      maxOfferSource: stats.maxOffer?.source,
      marketOfferCount: stats.offerCount,
      deltaPercent: comparison.deltaPercent,
      marketPosition: comparison.position,
      shoppingError,
      progressPhase: "comparing",
      progressMessage: `${i + 1}/${products.length} ürün karşılaştırıldı`,
    }, webhookSecret);

    if (SHOPPING_DELAY_MS > 0) await new Promise((r) => setTimeout(r, SHOPPING_DELAY_MS));
  }
}

export async function runPriceBenchmarkCrawl(
  job: PriceBenchmarkJobData,
  webhookSecret: string,
  fetchDelayMs: number,
  serpApiKey: string,
  isCancelled?: () => Promise<boolean>,
): Promise<void> {
  const shoppingSearchEnabled = isShoppingBrowserEnabled() || !!serpApiKey;

  // Aşama 1: Ürünleri keşfet ve listele
  const discovered = await discoverProducts(job, webhookSecret, fetchDelayMs, isCancelled);
  if (isCancelled && await isCancelled()) return;

  await postWebhook({
    priceBenchmarkRunEntityId: job.priceBenchmarkRunEntityId,
    event: "discover-complete",
    totalProducts: discovered.length,
    progressPhase: "discover-complete",
    progressMessage: discovered.length > 0
      ? `${discovered.length} ürün bulundu — fiyat karşılaştırması başlıyor…`
      : "Ürün bulunamadı",
  }, webhookSecret);

  if (discovered.length === 0) {
    await postWebhook({
      priceBenchmarkRunEntityId: job.priceBenchmarkRunEntityId,
      event: "complete",
      totalProducts: 0,
      serpApiConfigured: shoppingSearchEnabled,
    }, webhookSecret);
    return;
  }

  // Aşama 2: Google Shopping fiyat karşılaştırması
  await compareDiscoveredProducts(job, discovered, webhookSecret, serpApiKey, isCancelled);
  if (isCancelled && await isCancelled()) return;

  await postWebhook({
    priceBenchmarkRunEntityId: job.priceBenchmarkRunEntityId,
    event: "complete",
    totalProducts: discovered.length,
    serpApiConfigured: shoppingSearchEnabled,
  }, webhookSecret);
}

export async function failPriceBenchmarkCrawl(
  runEntityId: string,
  errorMessage: string,
  webhookSecret: string,
): Promise<void> {
  await postWebhook({
    priceBenchmarkRunEntityId: runEntityId,
    event: "failed",
    errorMessage,
  }, webhookSecret);
}
