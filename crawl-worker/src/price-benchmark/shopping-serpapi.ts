import {
  aggregateShoppingOffers,
  buildShoppingQuery,
  compareToMarket,
  parsePriceValue,
  type ShoppingOffer,
  type ShoppingPriceStats,
} from "./shopping-types.js";

export type { ShoppingOffer, ShoppingPriceStats };
export { compareToMarket };

export async function searchGoogleShoppingSerpApi(
  name: string,
  brand: string | undefined,
  apiKey: string,
): Promise<ShoppingPriceStats> {
  const empty = aggregateShoppingOffers([]);

  if (!apiKey.trim()) return empty;

  const q = buildShoppingQuery(name, brand);
  const url = new URL("https://serpapi.com/search.json");
  url.searchParams.set("engine", "google_shopping");
  url.searchParams.set("q", q);
  url.searchParams.set("api_key", apiKey);
  url.searchParams.set("gl", "tr");
  url.searchParams.set("hl", "tr");
  url.searchParams.set("num", "20");

  const response = await fetch(url.toString());
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`SerpAPI ${response.status}: ${text.slice(0, 200)}`);
  }

  const data = await response.json() as {
    shopping_results?: Array<{
      position?: number;
      title?: string;
      price?: string;
      extracted_price?: number;
      link?: string;
      product_link?: string;
      source?: string;
      thumbnail?: string;
      serpapi_thumbnail?: string;
    }>;
  };

  const offers: ShoppingOffer[] = [];
  for (const row of data.shopping_results ?? []) {
    const price = parsePriceValue(row.extracted_price) ?? parsePriceValue(row.price);
    if (price == null) continue;
    offers.push({
      position: row.position ?? offers.length + 1,
      title: row.title ?? "",
      price,
      currency: "TRY",
      link: row.product_link ?? row.link ?? "",
      source: row.source ?? "",
      thumbnail: row.serpapi_thumbnail ?? row.thumbnail ?? "",
    });
  }

  return aggregateShoppingOffers(offers);
}
