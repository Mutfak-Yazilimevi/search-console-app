import { readFileSync, readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));

export interface SeoRule {
  id: string;
  category: string;
  severity: "critical" | "warning" | "info";
  message: string;
  fixHint?: string;
  docUrl?: string;
}

export function loadAllRules(): Map<string, SeoRule> {
  const rulesDir = process.env.RULES_DIR ?? join(__dirname, "../../../docs/seo-rules");
  const map = new Map<string, SeoRule>();

  try {
    for (const file of readdirSync(rulesDir)) {
      if (!file.endsWith(".json")) continue;
      const items = JSON.parse(readFileSync(join(rulesDir, file), "utf8")) as SeoRule[];
      for (const rule of items) map.set(rule.id, rule);
    }
  } catch {
    map.set("meta-title-missing", {
      id: "meta-title-missing",
      category: "meta-tags",
      severity: "critical",
      message: "Sayfada <title> etiketi eksik.",
    });
  }

  return map;
}
