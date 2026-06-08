import type { SeoRule } from "./rule-loader.js";

export interface CrawlIssue {
  ruleId: string;
  category: string;
  severity: "Critical" | "Warning" | "Info";
  message: string;
  evidence?: string;
  fixHint?: string;
  docUrl?: string;
}

export function severityToEnum(severity: SeoRule["severity"]): CrawlIssue["severity"] {
  switch (severity) {
    case "critical":
      return "Critical";
    case "warning":
      return "Warning";
    default:
      return "Info";
  }
}

export function makeIssue(rule: SeoRule, evidence?: string): CrawlIssue {
  return {
    ruleId: rule.id,
    category: rule.category,
    severity: severityToEnum(rule.severity),
    message: rule.message,
    evidence,
    fixHint: rule.fixHint,
    docUrl: rule.docUrl,
  };
}

export function mapRuleSeverityToWebhook(severity: SeoRule["severity"]): CrawlIssue["severity"] {
  return severityToEnum(severity);
}
