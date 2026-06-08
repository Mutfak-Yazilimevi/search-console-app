export interface AuditRunDto {
  entityId: string;
  inputUrl: string;
  normalizedUrl: string;
  status: string;
  mode: string;
  createdAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  pagesCrawled: number;
  issuesFound: number;
  criticalCount: number;
  warningCount: number;
  infoCount: number;
  score?: number | null;
  errorMessage?: string | null;
  progressPhase?: string | null;
  progressMessage?: string | null;
  searchConsolePropertyUrl?: string | null;
}

export interface PerformanceQueryDto {
  query?: string | null;
  clicks: number;
  impressions: number;
  ctr: number;
  position: number;
}

export interface PerformanceDto {
  propertyUrl: string;
  totalClicks28d: number;
  totalImpressions28d: number;
  topQueries: PerformanceQueryDto[];
}

export interface ContentQualityDto {
  url: string;
  eeatScore: number;
  checklistJson: string;
  suggestionsJson?: string | null;
}

export interface ScannedPageDto {
  entityId: string;
  url: string;
  statusCode?: number | null;
  title?: string | null;
  metaDescription?: string | null;
  crawlDepth: number;
  responseTimeMs?: number | null;
  scannedAt: string;
}

export interface AuditIssueDto {
  entityId: string;
  pageUrl: string;
  ruleId: string;
  category: string;
  severity: string;
  source: string;
  message: string;
  evidence?: string | null;
  fixHint?: string | null;
  docUrl?: string | null;
  createdAt: string;
}

export interface AuditDetailDto {
  run: AuditRunDto;
  pages: ScannedPageDto[];
  issues: AuditIssueDto[];
}

export interface PageSpeedDto {
  url: string;
  performanceScore: number;
  lcp?: string | null;
  inp?: string | null;
  cls?: string | null;
  strategy: string;
}

export interface IndexStatusDto {
  available: boolean;
  domain?: string;
  estimatedIndexedPages?: number;
  crawledPages?: number;
  coverageRatio?: number;
  source?: string;
}

export interface BacklinkDto {
  available: boolean;
  internalLinkCount?: number;
  uniqueInternalTargets?: number;
  orphanPageCount?: number;
  externalReferringDomainCount?: number | null;
  externalBacklinkCount?: number | null;
  externalSource?: string | null;
  externalTopDomains?: string[] | null;
}

export interface KeywordDto {
  keyword: string;
  position: number;
  impressions: number;
  clicks: number;
  ctr: number;
}

export interface KeywordSerpDto {
  keyword: string;
  position: number;
  matchedUrl?: string | null;
}

export interface KeywordWatchDto {
  entityId: string;
  siteHost: string;
  keyword: string;
  isEnabled?: boolean;
  createdAtUtc?: string;
}

export interface SearchConsoleSitemapDto {
  path: string;
  errors: number;
  warnings: number;
  isPending?: boolean;
}

export interface SearchConsoleCoverageDto {
  available: boolean;
  propertyUrl?: string;
  indexedPages?: number;
  excludedPages?: number;
  inspectedCount?: number;
  passedCount?: number;
  failedCount?: number;
  sitemaps?: SearchConsoleSitemapDto[];
}

export interface FaqItemDto {
  question: string;
  answer: string;
}

export interface FaqGenerationDto {
  pageUrl: string;
  questions: FaqItemDto[];
  htmlSection: string;
  jsonLd: string;
}

export interface MetaGenerationDto {
  pageUrl: string;
  title: string;
  metaDescription: string;
  titleTagHtml: string;
  metaTagHtml: string;
}

export interface AltTextSuggestionDto {
  src: string;
  altText: string;
  imgHtmlSnippet: string;
}

export interface AltTextGenerationDto {
  pageUrl: string;
  images: AltTextSuggestionDto[];
}
