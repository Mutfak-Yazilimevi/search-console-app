import { extractJsonLdBlocks } from "../rules/html-extractors.js";

type JsonObject = Record<string, unknown>;

export interface ExtractedProduct {
  url: string;
  name?: string;
  description?: string;
  sku?: string;
  visibleSku?: string;
  productId?: string;
  gtin?: string;
  mpn?: string;
  brand?: string;
  condition?: string;
  image?: string;
  images: string[];
  imageCount: number;
  schemaPrice?: number;
  schemaListPrice?: number;
  visiblePrice?: number;
  priceCurrency?: string;
  availability?: string;
  identifierExists?: boolean;
  itemGroupId?: string;
  color?: string;
  size?: string;
  hasProductSchema: boolean;
  hasMicrodataProduct: boolean;
  hasOgProductMeta: boolean;
  isNoindex: boolean;
  canonicalUrl?: string;
  canonicalPointsElsewhere: boolean;
  mainImageAltMissing: boolean;
  mainImageWidth?: number;
  mainImageHeight?: number;
  hasAggregateRating: boolean;
  hasVisibleReviewSection: boolean;
  isHttps: boolean;
  hasViewportMeta: boolean;
  googleProductCategory?: string;
  hasShippingDetails: boolean;
  hasReturnPolicy: boolean;
  priceValidUntil?: string;
  hasSalePrice: boolean;
  urlHasTrackingParams: boolean;
  imageLooksPromotional: boolean;
}

function getTypeName(node: JsonObject): string | null {
  const type = node["@type"];
  if (typeof type === "string") return type;
  if (Array.isArray(type)) {
    const first = type.find((t) => typeof t === "string");
    return typeof first === "string" ? first : null;
  }
  return null;
}

function collectNodes(root: unknown, out: JsonObject[]): void {
  if (!root || typeof root !== "object") return;
  if (Array.isArray(root)) {
    for (const item of root) collectNodes(item, out);
    return;
  }
  const obj = root as JsonObject;
  out.push(obj);
  if (Array.isArray(obj["@graph"])) {
    for (const item of obj["@graph"]) collectNodes(item, out);
  }
}

function parsePrice(val: unknown): number | undefined {
  if (typeof val === "number") return val;
  if (typeof val === "string") {
    const n = parseFloat(val.replace(/[^\d.,]/g, "").replace(",", "."));
    return Number.isFinite(n) ? n : undefined;
  }
  return undefined;
}

function extractOffer(offer: unknown): {
  price?: number;
  listPrice?: number;
  currency?: string;
  availability?: string;
} {
  const obj = Array.isArray(offer) ? offer[0] : offer;
  if (!obj || typeof obj !== "object") return {};
  const o = obj as JsonObject;
  const price = parsePrice(o.price ?? o.lowPrice);
  const listPrice = parsePrice(o.highPrice ?? o.priceSpecification);
  return {
    price,
    listPrice: listPrice && price && listPrice > price ? listPrice : parsePrice(o.highPrice),
    currency: typeof o.priceCurrency === "string" ? o.priceCurrency : undefined,
    availability: typeof o.availability === "string"
      ? o.availability.split("/").pop()
      : undefined,
  };
}

function normalizePageUrl(url: string): string {
  try {
    const u = new URL(url);
    return `${u.protocol}//${u.host}${u.pathname}`.replace(/\/$/, "").toLowerCase();
  } catch {
    return url.trim().replace(/\/$/, "").toLowerCase();
  }
}

function extractVisiblePrice(html: string): number | undefined {
  const patterns = [
    /(?:₺|TL|TRY)\s*([\d.,]+)/i,
    /([\d.,]+)\s*(?:₺|TL|TRY)/i,
    /"price"\s*:\s*"?([\d.,]+)"?/i,
    /class="[^"]*price[^"]*"[^>]*>([^<]*[\d.,]+[^<]*)</i,
  ];
  for (const p of patterns) {
    const m = html.match(p);
    if (m?.[1]) {
      const n = parseFloat(m[1].replace(/\./g, "").replace(",", "."));
      if (Number.isFinite(n) && n > 0) return n;
    }
  }
  return undefined;
}

function urlHasTrackingParams(pageUrl: string): boolean {
  try {
    const u = new URL(pageUrl);
    const keys = [...u.searchParams.keys()].map((k) => k.toLowerCase());
    return keys.some((k) =>
      k.includes("session") || k.includes("sid") || k === "fbclid" || k === "gclid" || k.startsWith("utm_"));
  } catch {
    return false;
  }
}

function titleHasPromoSpam(title?: string): boolean {
  if (!title) return false;
  return /(?:ücretsiz\s*kargo|%\s*\d+\s*indirim|en\s*ucuz|taksit\s*0|hemen\s*al|fırsat|kampanya|🔥|!!!)/i.test(title);
}

function titleMostlyCaps(title?: string): boolean {
  if (!title || title.length < 8) return false;
  const letters = title.replace(/[^A-Za-zÇĞİÖŞÜçğıöşü]/g, "");
  if (letters.length < 6) return false;
  const upper = letters.replace(/[^A-ZÇĞİÖŞÜ]/g, "").length;
  return upper / letters.length > 0.6;
}

function imageLooksPromotional(imageUrl?: string): boolean {
  if (!imageUrl) return false;
  const lower = imageUrl.toLowerCase();
  return /watermark|[_-]wm[_-]|[_-]promo|promo[_-]|banner|overlay|stamp|badge|indirim|kampanya|fırsat|[-_]sale|coupon|placeholder|logo[-_]only|text[-_]overlay/.test(lower);
}

export function isProductUrl(url: string): boolean {
  const lower = url.toLowerCase();
  return /\/(product|products|urun|urunler|p|item|shop)\//.test(lower)
    || /[-_/](p|product|urun)[-_/]/i.test(lower);
}

export function extractProductFromPage(url: string, html: string): ExtractedProduct | null {
  const nodes: JsonObject[] = [];
  for (const block of extractJsonLdBlocks(html)) {
    try {
      collectNodes(JSON.parse(block), nodes);
    } catch {
      // skip invalid json-ld
    }
  }

  const productNode = nodes.find((n) => getTypeName(n) === "Product");
  const hasProductSchema = !!productNode;
  if (!hasProductSchema && !isProductUrl(url)) return null;

  const offer = productNode ? extractOffer(productNode.offers) : {};
  const images: string[] = [];
  let image: string | undefined;

  if (productNode?.image) {
    if (typeof productNode.image === "string") {
      image = productNode.image;
      images.push(productNode.image);
    } else if (Array.isArray(productNode.image)) {
      for (const img of productNode.image) {
        if (typeof img === "string") images.push(img);
        else if (img && typeof img === "object" && typeof (img as JsonObject).url === "string")
          images.push((img as JsonObject).url as string);
      }
      image = images[0];
    } else if (typeof productNode.image === "object" && typeof (productNode.image as JsonObject).url === "string") {
      image = (productNode.image as JsonObject).url as string;
      images.push(image);
    }
  }

  const ogImage = html.match(/<meta[^>]+property=["']og:image["'][^>]+content=["']([^"']+)["']/i)?.[1]
    ?? html.match(/<meta[^>]+content=["']([^"']+)["'][^>]+property=["']og:image["']/i)?.[1];
  if (!image && ogImage) {
    image = ogImage;
    images.push(ogImage);
  }

  const hasOgProductMeta = /<meta[^>]+property=["']og:(?:price:amount|product)["']/i.test(html)
    || /<meta[^>]+property=["']product:price:amount["']/i.test(html);

  const hasMicrodataProduct = /itemtype=["'][^"']*schema\.org\/Product["']/i.test(html);

  const isNoindex = /<meta[^>]+name=["'](?:robots|googlebot)["'][^>]+content=["'][^"']*noindex/i.test(html)
    || /<meta[^>]+content=["'][^"']*noindex[^"']*["'][^>]+name=["'](?:robots|googlebot)["']/i.test(html);

  const canonical = html.match(/<link[^>]+rel=["']canonical["'][^>]+href=["']([^"']+)["']/i)?.[1]
    ?? html.match(/<link[^>]+href=["']([^"']+)["'][^>]+rel=["']canonical["']/i)?.[1];

  const canonicalPointsElsewhere = !!canonical
    && normalizePageUrl(canonical) !== normalizePageUrl(url);

  const mainImageAltMissing = !!image
    && !/<img[^>]+alt=["'][^"']+["'][^>]*>/i.test(html);

  const visibleSku = html.match(/(?:SKU|Stok Kodu|Ürün Kodu)\s*[:#]?\s*([A-Za-z0-9\-_.]+)/i)?.[1];

  const hasAggregateRating = !!productNode?.aggregateRating
    || /"@type"\s*:\s*"AggregateRating"/i.test(html);

  const hasVisibleReviewSection = /(?:yorum|review|değerlendirme|rating)/i.test(html)
    && /(?:class=["'][^"']*(?:review|rating|yorum)[^"']*["']|itemprop=["']review)/i.test(html);

  const hasViewportMeta = /<meta[^>]+name=["']viewport["']/i.test(html);

  const googleProductCategory = typeof productNode?.category === "string"
    ? productNode.category as string
    : typeof productNode?.googleProductCategory === "string"
      ? productNode.googleProductCategory as string
      : undefined;

  const offerObj = productNode?.offers
    ? (Array.isArray(productNode.offers) ? productNode.offers[0] : productNode.offers) as JsonObject | undefined
    : undefined;

  const hasShippingDetails = !!offerObj?.shippingDetails
    || /shippingDetails|deliveryTime|ShippingDeliveryTime/i.test(html);

  const hasReturnPolicy = !!productNode?.hasMerchantReturnPolicy
    || !!offerObj?.hasMerchantReturnPolicy
    || /hasMerchantReturnPolicy|MerchantReturnPolicy/i.test(html);

  const priceValidUntil = typeof offerObj?.priceValidUntil === "string"
    ? offerObj.priceValidUntil as string
    : undefined;

  const uri = new URL(url);
  const visiblePrice = extractVisiblePrice(html);

  const hasSalePrice = (visiblePrice != null && offer.price != null && visiblePrice < offer.price)
    || (offer.listPrice != null && offer.price != null && offer.listPrice > offer.price);

  const identifierExistsRaw = productNode?.identifierExists;
  const identifierExists = typeof identifierExistsRaw === "boolean"
    ? identifierExistsRaw
    : typeof identifierExistsRaw === "string"
      ? identifierExistsRaw.toLowerCase() === "true"
      : undefined;

  const imageCount = new Set(images.filter(Boolean)).size;

  return {
    url,
    name: typeof productNode?.name === "string" ? productNode.name : undefined,
    description: typeof productNode?.description === "string" ? productNode.description : undefined,
    sku: typeof productNode?.sku === "string" ? productNode.sku : undefined,
    visibleSku,
    productId: typeof productNode?.productID === "string" ? productNode.productID as string
      : typeof productNode?.["@id"] === "string" ? productNode["@id"] as string : undefined,
    gtin: typeof productNode?.gtin === "string" ? productNode.gtin
      : typeof productNode?.gtin13 === "string" ? productNode.gtin13 as string : undefined,
    mpn: typeof productNode?.mpn === "string" ? productNode.mpn : undefined,
    brand: typeof productNode?.brand === "string" ? productNode.brand
      : productNode?.brand && typeof productNode.brand === "object"
        ? (productNode.brand as JsonObject).name as string
        : undefined,
    condition: typeof productNode?.itemCondition === "string"
      ? (productNode.itemCondition as string).split("/").pop()
      : undefined,
    image,
    images: [...new Set(images)],
    imageCount,
    schemaPrice: offer.price,
    schemaListPrice: offer.listPrice,
    visiblePrice,
    priceCurrency: offer.currency,
    availability: offer.availability,
    identifierExists,
    itemGroupId: typeof productNode?.inProductGroupWithID === "string"
      ? productNode.inProductGroupWithID as string
      : typeof productNode?.itemGroupId === "string" ? productNode.itemGroupId as string : undefined,
    color: typeof productNode?.color === "string" ? productNode.color : undefined,
    size: typeof productNode?.size === "string" ? productNode.size : undefined,
    hasProductSchema,
    hasMicrodataProduct,
    hasOgProductMeta,
    isNoindex,
    canonicalUrl: canonical,
    canonicalPointsElsewhere,
    mainImageAltMissing,
    hasAggregateRating,
    hasVisibleReviewSection,
    isHttps: uri.protocol === "https:",
    hasViewportMeta,
    googleProductCategory,
    hasShippingDetails,
    hasReturnPolicy,
    priceValidUntil,
    hasSalePrice,
    urlHasTrackingParams: urlHasTrackingParams(url),
    imageLooksPromotional: imageLooksPromotional(image),
  };
}
