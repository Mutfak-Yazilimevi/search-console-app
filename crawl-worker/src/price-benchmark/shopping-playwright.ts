import type { Locator, Page } from "playwright";
import { getBrowser } from "../playwright/render.js";
import {
  aggregateShoppingOffers,
  parsePriceValue,
  type ShoppingOffer,
  type ShoppingPriceStats,
} from "./shopping-types.js";

const SHOPPING_ENABLED = process.env.PLAYWRIGHT_ENABLED !== "false"
  && process.env.SHOPPING_BROWSER_ENABLED !== "false";

const MAX_OFFERS = Number(process.env.SHOPPING_MAX_OFFERS ?? "20");

const SHOPPING_HOME_URLS = [
  "https://www.google.com/shopping",
  "https://shopping.google.com",
];

/** Kırılım bazlı — #tsf id yerine form > RNNXgb > SDkEP > a4bIc > textarea hiyerarşisi */
const SEARCH_BOX_SELECTORS = [
  "form div.RNNXgb div.SDkEP div.a4bIc textarea",
  "div.RNNXgb div.SDkEP textarea",
  "div.RNNXgb textarea",
  "form textarea[name='q']",
  "textarea[name='q']",
  "form textarea",
  "input[name='q']",
  "[role='combobox']",
];

const CHROME_UA =
  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

export function isShoppingBrowserEnabled(): boolean {
  return SHOPPING_ENABLED;
}

export class GoogleShoppingBlockedError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "GoogleShoppingBlockedError";
  }
}

async function detectGoogleBlock(page: Page): Promise<string | null> {
  return page.evaluate(() => {
    const text = document.body?.innerText ?? "";
    const url = location.href;
    if (/google\.com\/sorry|\/sorry\/index/.test(url)) {
      return "Google CAPTCHA engelledi (Docker/sunucu IP'si)";
    }
    if (/sıra dışı bir trafik|unusual traffic|not a robot|doğrulama|recaptcha|captcha/i.test(text)) {
      return "Google CAPTCHA engelledi (otomatik trafik algılandı)";
    }
    if (/Bu sayfa hakkında/.test(text) && text.length < 800) {
      return "Google erişimi engelledi";
    }
    return null;
  });
}

async function dismissGoogleConsent(page: Page): Promise<void> {
  const selectors = [
    'button:has-text("Tümünü kabul et")',
    'button:has-text("Kabul et")',
    'button:has-text("Accept all")',
    'button:has-text("Accept")',
    "form[action*='consent'] button",
  ];
  for (const selector of selectors) {
    try {
      const btn = page.locator(selector).first();
      if (await btn.isVisible({ timeout: 1200 })) {
        await btn.click({ timeout: 3000 });
        await page.waitForTimeout(800);
        return;
      }
    } catch {
      // try next
    }
  }
}

async function findSearchBox(page: Page): Promise<Locator | null> {
  for (const selector of SEARCH_BOX_SELECTORS) {
    const box = page.locator(selector).first();
    try {
      if (await box.isVisible({ timeout: 2000 })) return box;
    } catch {
      // try next selector
    }
  }
  return null;
}

async function openShoppingHome(page: Page): Promise<void> {
  let lastError: Error | null = null;

  for (const url of SHOPPING_HOME_URLS) {
    try {
      await page.goto(url, { waitUntil: "domcontentloaded", timeout: 40000 });
      await dismissGoogleConsent(page);
      await page.waitForTimeout(1500);

      const blockReason = await detectGoogleBlock(page);
      if (blockReason) {
        throw new GoogleShoppingBlockedError(
          `${blockReason}. SERP_API_KEY tanımlayın veya crawl-worker'ı yerel makinede çalıştırın.`,
        );
      }

      const searchBox = await findSearchBox(page);
      if (searchBox) return;

      lastError = new Error(`Arama kutusu bulunamadı: ${url}`);
    } catch (err) {
      lastError = err instanceof Error ? err : new Error(String(err));
      if (err instanceof GoogleShoppingBlockedError) throw err;
    }
  }

  throw lastError ?? new Error("Google Shopping açılamadı");
}

async function searchViaTextbox(page: Page, query: string): Promise<void> {
  const searchBox = await findSearchBox(page);
  if (!searchBox) {
    throw new Error("Shopping arama kutusu bulunamadı (textarea)");
  }

  await searchBox.click({ timeout: 5000 });
  await searchBox.fill("");
  await searchBox.fill(query);
  await page.waitForTimeout(400);
  await searchBox.press("Enter");

  await page.waitForLoadState("domcontentloaded").catch(() => undefined);
  await page.waitForTimeout(2500);

  const blockReason = await detectGoogleBlock(page);
  if (blockReason) {
    throw new GoogleShoppingBlockedError(
      `${blockReason}. SERP_API_KEY tanımlayın veya crawl-worker'ı yerel makinede çalıştırın.`,
    );
  }
}

function normalizeGoogleLink(link: string): string {
  if (!link) return "";
  if (link.startsWith("/")) return `https://www.google.com${link}`;
  return link;
}

/** Kırılım bazlı DOM kazıma — id/data-* attribute yerine hiyerarşi */
async function extractOffersFromPage(page: Page): Promise<ShoppingOffer[]> {
  const domOffers = await page.evaluate((maxOffers) => {
    type Raw = { title: string; price: string; source: string; link: string; thumbnail: string };
    const results: Raw[] = [];
    const seen = new Set<string>();

    const parsePrice = (text: string): string => {
      const m = text.match(/([\d][\d.,]*)\s*(?:₺|TL|TRY)/i)
        ?? text.match(/(?:₺|TL|TRY)\s*([\d][\d.,]*)/i);
      return m?.[1] ?? "";
    };

    /** Ürün kartı: link > liste öğesi veya ürün satırı kapsayıcısı */
    const findCardRoot = (anchor: Element): Element | null => {
      return anchor.closest("li")
        ?? anchor.closest("[role='listitem']")
        ?? anchor.closest("div[class*='sh-dgr']")
        ?? anchor.closest("div[class*='product']")
        ?? anchor.parentElement?.parentElement?.parentElement
        ?? anchor.parentElement?.parentElement
        ?? anchor.parentElement;
    };

    const extractFromCard = (card: Element, fallbackLink = ""): void => {
      const linkEl = card.querySelector('a[href*="shopping/product"]') as HTMLAnchorElement | null
        ?? (fallbackLink.includes("shopping/product") ? { href: fallbackLink } as HTMLAnchorElement : null);
      const link = linkEl?.href ?? fallbackLink;
      if (!link.includes("shopping/product")) return;

      const title = linkEl?.getAttribute("aria-label")?.trim()
        ?? card.querySelector("h3, h4, h2, [role='heading']")?.textContent?.trim()
        ?? linkEl?.textContent?.trim()
        ?? "";
      if (title.length < 4) return;

      const priceNode = card.querySelector(
        "[aria-label*='₺'], [aria-label*='TL'], span[class*='price'], div[class*='price']",
      );
      const priceText = priceNode?.getAttribute("aria-label")
        ?? priceNode?.textContent
        ?? card.textContent
        ?? "";
      const price = parsePrice(priceText);
      if (!price) return;

      let source = "";
      const storeNode = card.querySelector(
        "span[class*='store'], div[class*='store'], span[class*='merchant'], div[class*='source']",
      );
      if (storeNode) {
        const t = storeNode.textContent?.trim() ?? "";
        if (t.length >= 2 && t.length <= 60) source = t;
      }
      if (!source) {
        for (const span of card.querySelectorAll("span, div")) {
          const t = span.textContent?.trim() ?? "";
          if (t.length >= 2 && t.length <= 50 && !/₺|TL|\d+[.,]\d+/.test(t) && t !== title) {
            source = t;
            break;
          }
        }
      }

      const img = card.querySelector("img[src]") as HTMLImageElement | null;
      const thumbnail = img?.src ?? "";

      const key = `${title}|${price}|${link}`;
      if (seen.has(key)) return;
      seen.add(key);
      results.push({ title, price, source, link, thumbnail });
    };

    const main = document.querySelector("main, [role='main'], #search, #center_col, body") ?? document.body;
    const productAnchors = main.querySelectorAll('a[href*="shopping/product"]');

    for (const anchor of productAnchors) {
      const card = findCardRoot(anchor);
      if (card) extractFromCard(card, (anchor as HTMLAnchorElement).href);
      if (results.length >= maxOffers) break;
    }

    if (results.length === 0) {
      const gridCards = main.querySelectorAll(
        "li, [role='listitem'], div[class*='sh-dgr'], div[class*='pla-unit']",
      );
      for (const card of gridCards) {
        if (!card.querySelector('a[href*="shopping/product"]')) continue;
        extractFromCard(card);
        if (results.length >= maxOffers) break;
      }
    }

    return results.slice(0, maxOffers);
  }, MAX_OFFERS);

  const offers: ShoppingOffer[] = [];
  for (const row of domOffers) {
    const price = parsePriceValue(row.price);
    if (!row.title || price == null) continue;
    offers.push({
      position: offers.length + 1,
      title: row.title,
      price,
      currency: "TRY",
      link: normalizeGoogleLink(row.link),
      source: row.source,
      thumbnail: row.thumbnail,
    });
  }
  return offers;
}

export async function searchGoogleShoppingBrowser(
  productName: string,
  _brand?: string,
): Promise<ShoppingPriceStats> {
  const empty = aggregateShoppingOffers([]);

  if (!SHOPPING_ENABLED) return empty;

  const name = productName.trim();
  if (!name) return empty;

  const browser = await getBrowser();
  const context = await browser.newContext({
    locale: "tr-TR",
    userAgent: CHROME_UA,
    viewport: { width: 1920, height: 911 },
  });
  const page = await context.newPage();

  try {
    await openShoppingHome(page);
    await searchViaTextbox(page, name);

    try {
      await page.waitForSelector(
        'a[href*="shopping/product"], li a[href*="product"], [role="listitem"] a',
        { timeout: 15000 },
      );
    } catch {
      // devam et
    }

    await page.evaluate(() => window.scrollBy(0, 800));
    await page.waitForTimeout(1200);
    await page.evaluate(() => window.scrollBy(0, 800));
    await page.waitForTimeout(800);

    const offers = await extractOffersFromPage(page);
    return aggregateShoppingOffers(offers);
  } finally {
    await context.close();
  }
}
