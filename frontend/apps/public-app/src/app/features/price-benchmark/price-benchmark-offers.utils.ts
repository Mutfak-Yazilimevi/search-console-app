import { PriceBenchmarkItemDto } from './price-benchmark.models';

export interface ShoppingOfferView {
  position: number;
  title: string;
  price: number;
  currency: string;
  link: string;
  source: string;
  thumbnail: string;
}

export function parseShoppingOffers(json?: string | null): ShoppingOfferView[] {
  if (!json?.trim()) return [];
  try {
    const parsed = JSON.parse(json);
    if (!Array.isArray(parsed)) return [];
    return parsed
      .map((row): ShoppingOfferView | null => {
        const price = typeof row.price === 'number' ? row.price : null;
        if (price == null || price <= 0) return null;
        return {
          position: typeof row.position === 'number' ? row.position : 0,
          title: typeof row.title === 'string' ? row.title : '',
          price,
          currency: typeof row.currency === 'string' ? row.currency : 'TRY',
          link: typeof row.link === 'string' ? row.link : '',
          source: typeof row.source === 'string' ? row.source : '',
          thumbnail: typeof row.thumbnail === 'string' ? row.thumbnail : '',
        };
      })
      .filter((o): o is ShoppingOfferView => o != null)
      .sort((a, b) => a.position - b.position);
  } catch {
    return [];
  }
}

export function getMinOffer(item: PriceBenchmarkItemDto, offers?: ShoppingOfferView[]): ShoppingOfferView | null {
  const list = offers ?? parseShoppingOffers(item.shoppingOffersJson);
  if (!list.length) return null;
  if (item.minMarketPrice != null) {
    const match = list.find((o) => o.price === item.minMarketPrice);
    if (match) return match;
  }
  return list.reduce((min, o) => (o.price < min.price ? o : min), list[0]!);
}

export function getMaxOffer(item: PriceBenchmarkItemDto, offers?: ShoppingOfferView[]): ShoppingOfferView | null {
  const list = offers ?? parseShoppingOffers(item.shoppingOffersJson);
  if (!list.length) return null;
  if (item.maxMarketPrice != null) {
    const match = list.find((o) => o.price === item.maxMarketPrice);
    if (match) return match;
  }
  return list.reduce((max, o) => (o.price > max.price ? o : max), list[0]!);
}
