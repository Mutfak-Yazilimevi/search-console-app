export interface PriceBenchmarkRunDto {
  entityId: string;
  inputUrl: string;
  normalizedUrl: string;
  status: string;
  createdAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  totalProducts: number;
  serpApiConfigured: boolean;
  errorMessage?: string | null;
  progressPhase?: string | null;
  progressMessage?: string | null;
}

export interface PriceBenchmarkItemDto {
  entityId: string;
  pageUrl: string;
  title?: string | null;
  ourPrice?: number | null;
  priceCurrency?: string | null;
  minMarketPrice?: number | null;
  maxMarketPrice?: number | null;
  weightedAvgMarketPrice?: number | null;
  minOfferLink?: string | null;
  minOfferSource?: string | null;
  maxOfferLink?: string | null;
  maxOfferSource?: string | null;
  marketOfferCount: number;
  deltaPercent?: number | null;
  marketPosition: string;
  shoppingError?: string | null;
  shoppingOffersJson?: string | null;
}

export interface PriceBenchmarkDetailDto {
  run: PriceBenchmarkRunDto;
  products: PriceBenchmarkItemDto[];
}
