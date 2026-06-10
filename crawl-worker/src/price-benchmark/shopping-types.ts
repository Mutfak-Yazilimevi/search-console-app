export interface ShoppingOffer {
  position: number;
  title: string;
  price: number;
  currency: string;
  link: string;
  source: string;
  thumbnail: string;
}

export interface ShoppingPriceStats {
  offers: ShoppingOffer[];
  minPrice: number | null;
  maxPrice: number | null;
  weightedAvgPrice: number | null;
  minOffer: ShoppingOffer | null;
  maxOffer: ShoppingOffer | null;
  offerCount: number;
}

export function parsePriceValue(raw: unknown): number | null {
  if (typeof raw === "number" && Number.isFinite(raw)) return raw;
  if (typeof raw !== "string") return null;
  const cleaned = raw.replace(/[^\d.,]/g, "");
  let normalized = cleaned;
  if (/\d+\.\d{3}(?:,\d+)?$/.test(cleaned)) {
    normalized = cleaned.replace(/\./g, "").replace(",", ".");
  } else {
    normalized = cleaned.replace(",", ".");
  }
  const n = parseFloat(normalized);
  return Number.isFinite(n) && n > 0 ? n : null;
}

export function buildShoppingQuery(name: string, brand?: string): string {
  const parts = [name.trim()];
  if (brand?.trim()) parts.push(brand.trim());
  return parts.join(" ");
}

export function aggregateShoppingOffers(offers: ShoppingOffer[]): ShoppingPriceStats {
  const empty: ShoppingPriceStats = {
    offers: [],
    minPrice: null,
    maxPrice: null,
    weightedAvgPrice: null,
    minOffer: null,
    maxOffer: null,
    offerCount: 0,
  };
  if (!offers.length) return empty;

  let minOffer = offers[0]!;
  let maxOffer = offers[0]!;
  let weightSum = 0;
  let weightedSum = 0;

  for (const offer of offers) {
    if (offer.price < minOffer.price) minOffer = offer;
    if (offer.price > maxOffer.price) maxOffer = offer;
    const w = 1 / Math.max(offer.position, 1);
    weightSum += w;
    weightedSum += offer.price * w;
  }

  return {
    offers,
    minPrice: minOffer.price,
    maxPrice: maxOffer.price,
    weightedAvgPrice: weightSum > 0 ? weightedSum / weightSum : null,
    minOffer,
    maxOffer,
    offerCount: offers.length,
  };
}

export function compareToMarket(
  ourPrice: number | undefined,
  weightedAvg: number | null,
): { deltaPercent: number | null; position: string } {
  if (ourPrice == null || ourPrice <= 0 || weightedAvg == null || weightedAvg <= 0) {
    return { deltaPercent: null, position: "unknown" };
  }
  const deltaPercent = ((ourPrice - weightedAvg) / weightedAvg) * 100;
  let position = "average";
  if (deltaPercent < -5) position = "below";
  else if (deltaPercent > 5) position = "above";
  return { deltaPercent: Math.round(deltaPercent * 10) / 10, position };
}
