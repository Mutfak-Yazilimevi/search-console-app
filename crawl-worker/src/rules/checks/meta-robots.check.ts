import type { SeoRule } from "../rule-loader.js";
import type { CrawlIssue } from "../issue-factory.js";
import { makeIssue } from "../issue-factory.js";
import {
  evidenceDataNosnippetInvalid,
  evidenceRobotsConflict,
  evidenceRobotsDirective,
} from "../page-evidence.js";

function extractRobotsMetaContent(html: string): string | null {
  const match = html.match(/<meta[^>]+name=["']robots["'][^>]+content=["']([^"']+)["']/i)
    ?? html.match(/<meta[^>]+content=["']([^"']+)["'][^>]+name=["']robots["']/i);
  return match?.[1]?.trim().toLowerCase() || null;
}

function extractGooglebotMetaContent(html: string): string | null {
  const match = html.match(/<meta[^>]+name=["']googlebot["'][^>]+content=["']([^"']+)["']/i)
    ?? html.match(/<meta[^>]+content=["']([^"']+)["'][^>]+name=["']googlebot["']/i);
  return match?.[1]?.trim().toLowerCase() || null;
}

function hasDirective(value: string | null, directive: string): boolean {
  if (!value) return false;
  return value.split(/[,;]/).map((p) => p.trim()).includes(directive);
}

export function checkMetaRobots(
  html: string,
  xRobotsHeader: string | null | undefined,
  rules: Map<string, SeoRule>,
): CrawlIssue[] {
  const issues: CrawlIssue[] = [];
  const metaRobots = extractRobotsMetaContent(html);
  const googlebotMeta = extractGooglebotMetaContent(html);
  const header = (xRobotsHeader ?? "").toLowerCase();

  const metaNoindex = hasDirective(metaRobots, "noindex") || hasDirective(googlebotMeta, "noindex");
  const headerNoindex = /noindex/i.test(header);
  const metaIndex = hasDirective(metaRobots, "index");
  const headerIndex = /(?:^|[,\s])index(?:[,\s]|$)/i.test(header);

  if (metaNoindex && headerIndex) {
    const rule = rules.get("robots-meta-header-conflict");
    if (rule) {
      issues.push(makeIssue(rule, evidenceRobotsConflict(
        `noindex (meta: ${metaRobots ?? googlebotMeta ?? "?"})`,
        xRobotsHeader ?? header,
      )));
    }
  } else if (metaIndex && headerNoindex) {
    const rule = rules.get("robots-meta-header-conflict");
    if (rule) {
      issues.push(makeIssue(rule, evidenceRobotsConflict(
        `index (meta: ${metaRobots ?? "?"})`,
        xRobotsHeader ?? header,
      )));
    }
  }

  if (hasDirective(metaRobots, "nosnippet") || hasDirective(googlebotMeta, "nosnippet") || /nosnippet/i.test(header)) {
    const rule = rules.get("robots-nosnippet-active");
    if (rule) {
      issues.push(makeIssue(rule, evidenceRobotsDirective(
        "nosnippet aktif",
        (metaRobots ?? googlebotMeta ?? header) || "nosnippet",
        "Snippet gösterimi istiyorsanız nosnippet kaldırın",
      )));
    }
  }

  const invalidNosnippet = [...html.matchAll(/\bdata-nosnippet\b/gi)].some((m) => {
    const before = html.slice(Math.max(0, m.index! - 80), m.index);
    const tagMatch = before.match(/<(\w+)[^>]*$/i);
    const tag = tagMatch?.[1]?.toLowerCase();
    return tag !== undefined && !["span", "div", "section"].includes(tag);
  });
  if (invalidNosnippet) {
    const rule = rules.get("data-nosnippet-invalid");
    if (rule) issues.push(makeIssue(rule, evidenceDataNosnippetInvalid()));
  }

  if (header && /max-snippet\s*:\s*0/i.test(header)) {
    const rule = rules.get("x-robots-max-snippet-zero");
    if (rule) {
      issues.push(makeIssue(rule, evidenceRobotsDirective(
        "max-snippet:0",
        xRobotsHeader ?? header,
        "Arama snippet'leri için max-snippet:0 kaldırın veya artırın",
      )));
    }
  }

  return issues;
}
