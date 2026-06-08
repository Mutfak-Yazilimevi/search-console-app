import type { SeoRule } from "./rule-loader.js";
import type { CrawlIssue } from "./issue-factory.js";
import { makeIssue } from "./issue-factory.js";
import {
  countBadLinks,
  countH1,
  countImagesWithoutAlt,
  extractBodyText,
  extractFirstParagraphText,
  extractHreflang,
  extractHtmlLang,
  extractJsonLdBlocks,
  extractMetaDescription,
  extractOgMeta,
  extractTitle,
  hasNoindex,
} from "./html-extractors.js";
import { checkStructuredData } from "./checks/structured-data.check.js";
import { checkMetaRobots } from "./checks/meta-robots.check.js";
import { checkEcommerceUrl } from "./checks/ecommerce.check.js";
import { checkSpamSignals } from "./checks/spam.check.js";
import { buildH1MultipleEvidence } from "./checks/h1.check.js";
import { buildImgAltMissingEvidence } from "./checks/img-alt.check.js";
import {
  evidenceBadLinks,
  evidenceBreadcrumbMissing,
  evidenceDataVocabulary,
  evidenceDescriptionLength,
  evidenceGeoAiDisclosure,
  evidenceGeoFaqMissing,
  evidenceGeoSummaryMissing,
  evidenceHiddenText,
  evidenceH1Missing,
  evidenceHreflangMissingReturn,
  evidenceHreflangMissingXDefault,
  evidenceHtmlLangMissing,
  evidenceHttpStatus,
  evidenceHttpsRequired,
  evidenceImageNoSrcset,
  evidenceInterstitial,
  evidenceJsonLdInvalid,
  evidenceJsonLdMissing,
  evidenceKeywordStuffing,
  evidenceMaxImagePreviewMissing,
  evidenceMetaCharsetMissing,
  evidenceMetaDescriptionMissing,
  evidenceMetaKeywords,
  evidenceMixedContent,
  evidenceNoindex,
  evidenceOgImageSmall,
  evidenceOgMissing,
  evidenceProductSchemaMissing,
  evidenceThinContent,
  evidenceTitleMissing,
  evidenceTitleTooLong,
  evidenceTitleTooShort,
  evidenceTwitterCardMissing,
  evidenceUrlTooDeep,
  evidenceVideoSchemaMissing,
  evidenceViewportMissing,
  evidenceXRobotsNoindex,
  evidenceCanonicalMissing,
  evidenceCanonicalMultiple,
  evidenceFaviconMissing,
} from "./page-evidence.js";

export type { SeoRule } from "./rule-loader.js";
export type { CrawlIssue } from "./issue-factory.js";
export { loadAllRules } from "./rule-loader.js";
export { makeIssue, severityToEnum } from "./issue-factory.js";
export {
  countH1,
  extractCanonical,
  extractMetaDescription,
  extractTitle,
} from "./html-extractors.js";

export interface PageHeaders {
  xRobotsTag?: string | null;
}

export interface PageAnalyzeOptions extends PageHeaders {
  /** GEO / site-wide heuristics depth 0–1 sayfalarda çalıştırılır. */
  isShallowPage?: boolean;
}

export function analyzePage(
  url: string,
  statusCode: number,
  html: string,
  rules: Map<string, SeoRule>,
  options: PageAnalyzeOptions = {},
): CrawlIssue[] {
  const issues: CrawlIssue[] = [];
  const title = extractTitle(html);

  if (statusCode < 200 || statusCode >= 400) {
    const rule = rules.get("http-status-error");
    if (rule) issues.push(makeIssue(rule, evidenceHttpStatus(statusCode)));
  }

  if (!title) {
    const rule = rules.get("meta-title-missing");
    if (rule) issues.push(makeIssue(rule, evidenceTitleMissing()));
  } else if (title.length < 10) {
    const rule = rules.get("title-too-short");
    if (rule) issues.push(makeIssue(rule, evidenceTitleTooShort(title)));
  } else if (title.length > 70) {
    const rule = rules.get("title-too-long");
    if (rule) issues.push(makeIssue(rule, evidenceTitleTooLong(title)));
  }

  if (!extractMetaDescription(html)) {
    const rule = rules.get("meta-description-missing");
    if (rule) issues.push(makeIssue(rule, evidenceMetaDescriptionMissing()));
  }

  if (!/<meta[^>]+name=["']viewport["']/i.test(html)) {
    const rule = rules.get("viewport-missing");
    if (rule) issues.push(makeIssue(rule, evidenceViewportMissing()));
  }

  if (!url.startsWith("https://")) {
    const rule = rules.get("https-required");
    if (rule) issues.push(makeIssue(rule, evidenceHttpsRequired(url)));
  }

  const canonicalMatches = html.match(/<link[^>]+rel=["']canonical["']/gi) ?? [];
  if (canonicalMatches.length === 0) {
    const rule = rules.get("canonical-missing");
    if (rule) issues.push(makeIssue(rule, evidenceCanonicalMissing()));
  } else if (canonicalMatches.length > 1) {
    const rule = rules.get("canonical-multiple");
    if (rule) issues.push(makeIssue(rule, evidenceCanonicalMultiple(html)));
  }

  const h1Count = countH1(html);
  if (h1Count === 0) {
    const rule = rules.get("h1-missing");
    if (rule) issues.push(makeIssue(rule, evidenceH1Missing()));
  } else if (h1Count > 1) {
    const rule = rules.get("h1-multiple");
    if (rule) {
      issues.push(makeIssue(rule, buildH1MultipleEvidence(html, extractTitle(html))));
    }
  }

  if (hasNoindex(html)) {
    const rule = rules.get("noindex-detected");
    if (rule) issues.push(makeIssue(rule, evidenceNoindex(html)));
  }

  if (options.xRobotsTag && /noindex/i.test(options.xRobotsTag)) {
    const rule = rules.get("x-robots-noindex-header");
    if (rule) issues.push(makeIssue(rule, evidenceXRobotsNoindex(options.xRobotsTag)));
  }

  const missingAlt = countImagesWithoutAlt(html);
  if (missingAlt > 0) {
    const rule = rules.get("img-alt-missing");
    if (rule) issues.push(makeIssue(rule, buildImgAltMissingEvidence(html)));
  }

  if (!extractHtmlLang(html)) {
    const rule = rules.get("html-lang-missing");
    if (rule) issues.push(makeIssue(rule, evidenceHtmlLangMissing()));
  }

  const hreflangs = extractHreflang(html);
  if (hreflangs.length >= 2 && !hreflangs.includes("x-default")) {
    const rule = rules.get("hreflang-missing-x-default");
    if (rule) issues.push(makeIssue(rule, evidenceHreflangMissingXDefault(html)));
  }

  if (/data-vocabulary\.org/i.test(html)) {
    const rule = rules.get("data-vocabulary-deprecated");
    if (rule) issues.push(makeIssue(rule, evidenceDataVocabulary(html)));
  }

  const jsonLdBlocks = extractJsonLdBlocks(html);
  if (jsonLdBlocks.length === 0) {
    const rule = rules.get("json-ld-missing");
    if (rule) issues.push(makeIssue(rule, evidenceJsonLdMissing()));
  } else {
    for (const block of jsonLdBlocks) {
      try {
        JSON.parse(block);
      } catch {
        const rule = rules.get("json-ld-invalid");
        if (rule) issues.push(makeIssue(rule, evidenceJsonLdInvalid(block)));
        break;
      }
    }
  }

  const ogTitle = extractOgMeta(html, "og:title");
  const ogImage = extractOgMeta(html, "og:image");
  if (!ogTitle) {
    const rule = rules.get("og-title-missing");
    if (rule) issues.push(makeIssue(rule, evidenceOgMissing("title")));
  }
  if (!ogImage) {
    const rule = rules.get("og-image-missing");
    if (rule) issues.push(makeIssue(rule, evidenceOgMissing("image")));
  }

  const badLinks = countBadLinks(html);
  if (badLinks.javascript > 0) {
    const rule = rules.get("link-not-crawlable");
    if (rule) issues.push(makeIssue(rule, evidenceBadLinks("javascript", html, badLinks.javascript)));
  }
  if (badLinks.hashRoute > 0) {
    const rule = rules.get("js-hash-routing");
    if (rule) issues.push(makeIssue(rule, evidenceBadLinks("hash", html, badLinks.hashRoute)));
  }

  if (options.isShallowPage) {
    if (!/FAQPage|"@type"\s*:\s*"Question"/i.test(html)) {
      const rule = rules.get("geo-faq-missing");
      if (rule) issues.push(makeIssue(rule, evidenceGeoFaqMissing()));
    }

    const bodyText = extractBodyText(html);
    const firstPText = extractFirstParagraphText(html);
    if (bodyText.length > 300 && firstPText.length < 60) {
      const rule = rules.get("geo-summary-missing");
      if (rule) {
        const wordCount = bodyText.split(/\s+/).filter(Boolean).length;
        issues.push(makeIssue(rule, evidenceGeoSummaryMissing(firstPText, wordCount)));
      }
    }

    if (/generated by (chatgpt|openai|claude)|ai-generated content|yapay zeka/i.test(html)
      && !/ai-disclosure|yapay zeka.*bildir|generated with ai/i.test(html)) {
      const rule = rules.get("geo-ai-disclosure");
      if (rule) issues.push(makeIssue(rule, evidenceGeoAiDisclosure()));
    }
  }

  if (!/<meta[^>]+charset/i.test(html)) {
    const rule = rules.get("meta-charset-missing");
    if (rule) issues.push(makeIssue(rule, evidenceMetaCharsetMissing()));
  }

  if (/<meta[^>]+name=["']keywords["']/i.test(html)) {
    const rule = rules.get("meta-keywords-deprecated");
    if (rule) issues.push(makeIssue(rule, evidenceMetaKeywords(html)));
  }

  if (url.startsWith("https://") && /(?:src|href)=["']http:\/\//i.test(html)) {
    const rule = rules.get("mixed-content");
    if (rule) issues.push(makeIssue(rule, evidenceMixedContent(html)));
  }

  const bodyText = extractBodyText(html);
  const wordCount = bodyText.split(/\s+/).filter(Boolean).length;
  if (wordCount > 0 && wordCount < 300) {
    const rule = rules.get("thin-content");
    if (rule) issues.push(makeIssue(rule, evidenceThinContent(wordCount, bodyText.slice(0, 200))));
  }

  const desc = extractMetaDescription(html);
  if (desc) {
    if (desc.length < 50) {
      const rule = rules.get("description-too-short");
      if (rule) issues.push(makeIssue(rule, evidenceDescriptionLength(desc, "short")));
    } else if (desc.length > 160) {
      const rule = rules.get("description-too-long");
      if (rule) issues.push(makeIssue(rule, evidenceDescriptionLength(desc, "long")));
    }
  }

  if (title) {
    const words = title.toLowerCase().split(/\s+/);
    const freq = new Map<string, number>();
    for (const w of words) {
      if (w.length < 3) continue;
      freq.set(w, (freq.get(w) ?? 0) + 1);
    }
    for (const [w, c] of freq) {
      if (c >= 3) {
        const rule = rules.get("keyword-stuffing-title");
        if (rule) issues.push(makeIssue(rule, evidenceKeywordStuffing(w, c, title)));
        break;
      }
    }
  }

  if (/display\s*:\s*none|font-size\s*:\s*0|visibility\s*:\s*hidden/i.test(html)) {
    const rule = rules.get("hidden-text-css");
    if (rule) issues.push(makeIssue(rule, evidenceHiddenText(html)));
  }

  if (/class=["'][^"']*(modal|popup|interstitial|overlay)[^"']*["']/i.test(html)) {
    const rule = rules.get("intrusive-interstitial");
    if (rule) issues.push(makeIssue(rule, evidenceInterstitial(html)));
  }

  if (!/max-image-preview\s*:\s*large/i.test(html) && !/max-image-preview:large/i.test(html)) {
    const rule = rules.get("max-image-preview-missing");
    if (rule) issues.push(makeIssue(rule, evidenceMaxImagePreviewMissing()));
  }

  if (!/<link[^>]+rel=["'](?:shortcut )?icon["']/i.test(html) && !html.includes("/favicon.ico")) {
    const rule = rules.get("favicon-missing");
    if (rule) issues.push(makeIssue(rule, evidenceFaviconMissing()));
  }

  if (!extractOgMeta(html, "og:description")) {
    const rule = rules.get("og-description-missing");
    if (rule) issues.push(makeIssue(rule, evidenceOgMissing("description")));
  }

  if (!/<meta[^>]+name=["']twitter:card["']/i.test(html)) {
    const rule = rules.get("twitter-card-missing");
    if (rule) issues.push(makeIssue(rule, evidenceTwitterCardMissing()));
  }

  if (ogImage && !/1200|large|hero|og[-_]?(?:cover|image)/i.test(ogImage)) {
    const rule = rules.get("og-image-small");
    if (rule) issues.push(makeIssue(rule, evidenceOgImageSmall(ogImage)));
  }

  const pathDepth = new URL(url).pathname.split("/").filter(Boolean).length;
  if (pathDepth > 4) {
    const rule = rules.get("url-too-deep");
    if (rule) issues.push(makeIssue(rule, evidenceUrlTooDeep(url, pathDepth)));
  }

  if (/<(?:video|iframe[^>]+youtube|iframe[^>]+vimeo)/i.test(html)
    && !/VideoObject|"@type"\s*:\s*"VideoObject"/i.test(html)) {
    const rule = rules.get("video-schema-missing");
    if (rule) issues.push(makeIssue(rule, evidenceVideoSchemaMissing(html)));
  }

  const largeImgs = (html.match(/<img\b[^>]*>/gi) ?? []).filter((t) => !/\bsrcset=/i.test(t));
  if (largeImgs.length >= 3) {
    const rule = rules.get("image-no-srcset");
    if (rule) issues.push(makeIssue(rule, evidenceImageNoSrcset(html, largeImgs.length)));
  }

  if (/product|urun|shop|store/i.test(url) && !/"@type"\s*:\s*"Product"/i.test(html)) {
    const rule = rules.get("product-schema-missing");
    if (rule) issues.push(makeIssue(rule, evidenceProductSchemaMissing(url)));
  }

  if (!/"@type"\s*:\s*"BreadcrumbList"/i.test(html) && !/<nav[^>]+breadcrumb/i.test(html)) {
    const rule = rules.get("breadcrumb-missing");
    if (rule) issues.push(makeIssue(rule, evidenceBreadcrumbMissing()));
  }

  if (hreflangs.length >= 2) {
    const pageNorm = url.replace(/\/$/, "");
    const hasSelf = [...html.matchAll(/<link[^>]+hreflang=["']([^"']+)["'][^>]+href=["']([^"']+)["']/gi)]
      .some((m) => m[2].replace(/\/$/, "") === pageNorm);
    if (!hasSelf) {
      const rule = rules.get("hreflang-missing-return");
      if (rule) issues.push(makeIssue(rule, evidenceHreflangMissingReturn(url, html)));
    }
  }

  issues.push(...checkStructuredData(html, rules));
  issues.push(...checkMetaRobots(html, options.xRobotsTag, rules));
  issues.push(...checkEcommerceUrl(url, html, rules));
  issues.push(...checkSpamSignals(url, html, new URL(url).origin, rules));

  return issues;
}

export function extractLinks(html: string, origin: string, currentUrl: string): string[] {
  const links = new Set<string>();
  const hrefRegex = /<a\b[^>]*\bhref=["']([^"'#][^"']*)["']/gi;
  let match: RegExpExecArray | null;

  while ((match = hrefRegex.exec(html)) !== null) {
    try {
      const resolved = new URL(match[1], currentUrl);
      if (resolved.protocol !== "http:" && resolved.protocol !== "https:") continue;
      if (resolved.origin !== origin) continue;
      resolved.hash = "";
      links.add(resolved.href.replace(/\/$/, "") || resolved.origin);
    } catch {
      // skip invalid URLs
    }
  }

  return [...links];
}

export function extractAllInternalLinkTargets(html: string, origin: string, currentUrl: string): string[] {
  return extractLinks(html, origin, currentUrl);
}
