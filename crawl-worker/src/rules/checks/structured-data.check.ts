import type { SeoRule } from "../rule-loader.js";
import type { CrawlIssue } from "../issue-factory.js";
import { makeIssue } from "../issue-factory.js";
import { extractJsonLdBlocks } from "../html-extractors.js";
import { evidenceSchemaIssue } from "../page-evidence.js";

type JsonObject = Record<string, unknown>;

function getTypeName(node: JsonObject): string | null {
  const type = node["@type"];
  if (typeof type === "string") return type;
  if (Array.isArray(type)) {
    const first = type.find((t) => typeof t === "string");
    return typeof first === "string" ? first : null;
  }
  return null;
}

function hasNonEmptyString(node: JsonObject, key: string): boolean {
  const v = node[key];
  return typeof v === "string" && v.trim().length > 0;
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

function validateFaq(node: JsonObject): boolean {
  const main = node.mainEntity;
  if (!Array.isArray(main) || main.length === 0) return false;
  return main.some((item) => {
    if (!item || typeof item !== "object") return false;
    const q = item as JsonObject;
    if (getTypeName(q) !== "Question") return false;
    if (!hasNonEmptyString(q, "name")) return false;
    const answer = q.acceptedAnswer;
    return !!answer && typeof answer === "object" && hasNonEmptyString(answer as JsonObject, "text");
  });
}

function validateProduct(node: JsonObject): boolean {
  if (!hasNonEmptyString(node, "name")) return false;
  const hasImage = hasNonEmptyString(node, "image")
    || (Array.isArray(node.image) && node.image.length > 0);
  const offers = node.offers;
  const hasOffers = !!offers && (typeof offers === "object" || Array.isArray(offers));
  return hasImage && hasOffers;
}

function validateArticle(node: JsonObject): boolean {
  return hasNonEmptyString(node, "headline")
    && hasNonEmptyString(node, "datePublished")
    && hasAuthor(node);
}

function hasAuthor(node: JsonObject): boolean {
  const author = node.author;
  if (typeof author === "string") return author.trim().length > 0;
  if (Array.isArray(author)) {
    return author.some((a) =>
      typeof a === "string" ? a.trim().length > 0 : !!a && typeof a === "object" && hasNonEmptyString(a as JsonObject, "name"),
    );
  }
  return !!author && typeof author === "object" && hasNonEmptyString(author as JsonObject, "name");
}

function validateBreadcrumb(node: JsonObject): boolean {
  const items = node.itemListElement;
  return Array.isArray(items) && items.length > 0;
}

function validateLocalBusiness(node: JsonObject): boolean {
  const addr = node.address;
  const hasAddress = !!addr && typeof addr === "object";
  return hasAddress && (hasNonEmptyString(node, "telephone") || hasNonEmptyString(node, "name"));
}

function validateVideo(node: JsonObject): boolean {
  return hasNonEmptyString(node, "thumbnailUrl") && hasNonEmptyString(node, "uploadDate");
}

function validateOrganization(node: JsonObject): boolean {
  return hasNonEmptyString(node, "name") && hasNonEmptyString(node, "url");
}

function faqMissingFields(node: JsonObject): string[] {
  const missing: string[] = [];
  const main = node.mainEntity;
  if (!Array.isArray(main) || main.length === 0) return ["mainEntity (Question dizisi)"];
  const q = main[0] as JsonObject;
  if (getTypeName(q) !== "Question") missing.push("Question @type");
  if (!hasNonEmptyString(q, "name")) missing.push("Question.name");
  const answer = q.acceptedAnswer as JsonObject | undefined;
  if (!answer || !hasNonEmptyString(answer, "text")) missing.push("acceptedAnswer.text");
  return missing.length ? missing : ["geçerli FAQPage yapısı"];
}

function productMissingFields(node: JsonObject): string[] {
  const missing: string[] = [];
  if (!hasNonEmptyString(node, "name")) missing.push("name");
  if (!hasNonEmptyString(node, "image") && !(Array.isArray(node.image) && node.image.length > 0)) missing.push("image");
  if (!node.offers) missing.push("offers");
  return missing;
}

function articleMissingFields(node: JsonObject): string[] {
  const missing: string[] = [];
  if (!hasNonEmptyString(node, "headline")) missing.push("headline");
  if (!hasNonEmptyString(node, "datePublished")) missing.push("datePublished");
  if (!hasAuthor(node)) missing.push("author");
  return missing;
}

function findFirstNodeOfType(nodes: JsonObject[], type: string): JsonObject | null {
  return nodes.find((n) => getTypeName(n) === type) ?? null;
}

function validateOffer(node: JsonObject): boolean {
  const price = node.price ?? node.lowPrice;
  const hasPrice = price !== undefined && price !== null && String(price).trim() !== "";
  const availability = node.availability;
  const hasAvailability = typeof availability === "string" && availability.trim().length > 0;
  return hasPrice && hasAvailability;
}

export function checkStructuredData(html: string, rules: Map<string, SeoRule>): CrawlIssue[] {
  const issues: CrawlIssue[] = [];
  const blocks = extractJsonLdBlocks(html);
  if (blocks.length === 0) return issues;

  const nodes: JsonObject[] = [];
  for (const block of blocks) {
    try {
      collectNodes(JSON.parse(block), nodes);
    } catch {
      continue;
    }
  }

  const types = {
    FAQPage: false,
    Product: false,
    Article: false,
    NewsArticle: false,
    BlogPosting: false,
    BreadcrumbList: false,
    LocalBusiness: false,
    VideoObject: false,
    Organization: false,
  };

  let productOfferInvalid = false;
  let productReviewMissing = false;

  for (const node of nodes) {
    const type = getTypeName(node);
    if (!type) continue;

    switch (type) {
      case "FAQPage":
        types.FAQPage = validateFaq(node) || types.FAQPage;
        break;
      case "Product": {
        types.Product = validateProduct(node) || types.Product;
        const offers = node.offers;
        const offerNodes = Array.isArray(offers) ? offers : offers ? [offers] : [];
        if (offerNodes.length > 0) {
          const validOffer = offerNodes.some((o) => o && typeof o === "object" && validateOffer(o as JsonObject));
          if (!validOffer) productOfferInvalid = true;
        } else {
          productOfferInvalid = true;
        }
        const hasReview = !!node.aggregateRating || !!node.review
          || (Array.isArray(node.review) && node.review.length > 0);
        if (!hasReview) productReviewMissing = true;
        break;
      }
      case "Article":
      case "NewsArticle":
      case "BlogPosting":
        types.Article = validateArticle(node) || types.Article;
        break;
      case "BreadcrumbList":
        types.BreadcrumbList = validateBreadcrumb(node) || types.BreadcrumbList;
        break;
      case "LocalBusiness":
        types.LocalBusiness = validateLocalBusiness(node) || types.LocalBusiness;
        break;
      case "VideoObject":
        types.VideoObject = validateVideo(node) || types.VideoObject;
        break;
      case "Organization":
        types.Organization = validateOrganization(node) || types.Organization;
        break;
    }
  }

  const present = (t: string) => html.includes(`"@type"`) && (
    new RegExp(`"@type"\\s*:\\s*"${t}"`, "i").test(html)
    || new RegExp(`"@type"\\s*:\\s*\\[[^\\]]*"${t}"`, "i").test(html)
  );

  if (present("FAQPage") && !types.FAQPage) {
    const rule = rules.get("SD-FAQ-001");
    const node = findFirstNodeOfType(nodes, "FAQPage");
    if (rule) {
      issues.push(makeIssue(rule, evidenceSchemaIssue(
        "FAQPage",
        node ? faqMissingFields(node) : ["FAQPage"],
        "mainEntity içinde Question + acceptedAnswer ekleyin",
      )));
    }
  }
  if (present("Product") && !types.Product) {
    const rule = rules.get("SD-PROD-001");
    const node = findFirstNodeOfType(nodes, "Product");
    if (rule) {
      issues.push(makeIssue(rule, evidenceSchemaIssue(
        "Product",
        node ? productMissingFields(node) : ["Product"],
        "name, image ve offers alanlarını doldurun",
      )));
    }
  }
  if ((present("Article") || present("NewsArticle") || present("BlogPosting")) && !types.Article) {
    const rule = rules.get("SD-ART-001");
    const node = findFirstNodeOfType(nodes, "Article")
      ?? findFirstNodeOfType(nodes, "NewsArticle")
      ?? findFirstNodeOfType(nodes, "BlogPosting");
    if (rule) {
      issues.push(makeIssue(rule, evidenceSchemaIssue(
        getTypeName(node ?? {}) ?? "Article",
        node ? articleMissingFields(node) : ["Article"],
        "headline, datePublished ve author ekleyin",
      )));
    }
  }
  if (present("BreadcrumbList") && !types.BreadcrumbList) {
    const rule = rules.get("SD-BREAD-001");
    if (rule) {
      issues.push(makeIssue(rule, evidenceSchemaIssue(
        "BreadcrumbList",
        ["itemListElement"],
        "En az bir breadcrumb öğesi ekleyin",
      )));
    }
  }
  if (present("LocalBusiness") && !types.LocalBusiness) {
    const rule = rules.get("SD-LOCAL-001");
    if (rule) {
      issues.push(makeIssue(rule, evidenceSchemaIssue(
        "LocalBusiness",
        ["address", "name veya telephone"],
        "İşletme adresi ve iletişim bilgisi ekleyin",
      )));
    }
  }
  if (present("VideoObject") && !types.VideoObject) {
    const rule = rules.get("SD-VID-001");
    if (rule) {
      issues.push(makeIssue(rule, evidenceSchemaIssue(
        "VideoObject",
        ["thumbnailUrl", "uploadDate"],
        "Video küçük resmi ve yükleme tarihi ekleyin",
      )));
    }
  }
  if (present("Organization") && !types.Organization) {
    const rule = rules.get("SD-ORG-001");
    if (rule) {
      issues.push(makeIssue(rule, evidenceSchemaIssue(
        "Organization",
        ["name", "url"],
        "Kurum adı ve web sitesi URL'si ekleyin",
      )));
    }
  }
  if (present("Product") && productOfferInvalid) {
    const rule = rules.get("EC-001");
    if (rule) {
      issues.push(makeIssue(rule, evidenceSchemaIssue(
        "Product.offers",
        ["price", "availability"],
        "Offer içinde fiyat ve stok durumu (availability) belirtin",
      )));
    }
  }
  if (present("Product") && productReviewMissing) {
    const rule = rules.get("EC-002");
    if (rule) {
      issues.push(makeIssue(rule, evidenceSchemaIssue(
        "Product",
        ["aggregateRating veya review"],
        "Ürün değerlendirmesi veya yorum schema'sı ekleyin",
      )));
    }
  }

  return issues;
}
