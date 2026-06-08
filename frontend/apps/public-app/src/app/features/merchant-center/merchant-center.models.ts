export interface PriorityActionDto {
  ruleId: string;
  message: string;
  fixHint: string;
  affectedCount: number;
}

export interface ProductComplianceComparisonDto {
  previousRunEntityId: string;
  previousCompletedAt?: string | null;
  previousComplianceScore?: number | null;
  complianceScoreDelta: number;
  newCriticalRuleIds: string[];
  resolvedCriticalRuleIds: string[];
}

export interface ProductComplianceRunSummaryDto {
  entityId: string;
  inputUrl: string;
  status: string;
  analysisMode: string;
  createdAt: string;
  completedAt?: string | null;
  complianceScore?: number | null;
  totalProducts: number;
}

export interface ProductComplianceRunDto {
  entityId: string;
  inputUrl: string;
  normalizedUrl: string;
  status: string;
  analysisMode: string;
  createdAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  totalProducts: number;
  compliantCount: number;
  partialCount: number;
  nonCompliantCount: number;
  complianceScore?: number | null;
  siteReadinessScore?: number | null;
  criticalCount: number;
  warningCount: number;
  infoCount: number;
  errorMessage?: string | null;
  progressPhase?: string | null;
  progressMessage?: string | null;
  merchantCenterAccountId?: string | null;
  gmcSummary?: GmcRunSummaryDto | null;
  priorityActions: PriorityActionDto[];
  comparison?: ProductComplianceComparisonDto | null;
}

export interface GmcAggregateStatusDto {
  reportingContext?: string | null;
  approvedCount: number;
  pendingCount: number;
  disapprovedCount: number;
}

export interface GmcAccountIssueDto {
  title: string;
  detail?: string | null;
  severity?: string | null;
}

export interface GmcProductPerformanceDto {
  offerId: string;
  title?: string | null;
  clicks: number;
  impressions: number;
  clickThroughRate?: number | null;
}

export interface GmcRunSummaryDto {
  aggregateStatuses: GmcAggregateStatusDto[];
  accountIssues: GmcAccountIssueDto[];
  topPerformance?: GmcProductPerformanceDto[];
}

export interface ProductComplianceItemDto {
  entityId: string;
  pageUrl: string;
  title?: string | null;
  offerId?: string | null;
  gmcStatus?: string | null;
  complianceScore: number;
  status: string;
  issueCount: number;
}

export interface ProductComplianceIssueDto {
  entityId: string;
  pageUrl?: string | null;
  ruleId: string;
  field: string;
  severity: string;
  source: string;
  message: string;
  fixHint: string;
  docUrl?: string | null;
  gmcIssueCode?: string | null;
  evidence?: string | null;
}

export interface ProductComplianceDetailDto {
  run: ProductComplianceRunDto;
  products: ProductComplianceItemDto[];
  siteIssues: ProductComplianceIssueDto[];
  crossProductIssues: ProductComplianceIssueDto[];
  feedIssues: ProductComplianceIssueDto[];
}

export interface ProductComplianceProductDetailDto {
  product: ProductComplianceItemDto;
  issues: ProductComplianceIssueDto[];
}

export interface GmcAiSummaryDto {
  summaryMarkdown: string;
  priorities: string[];
}

export interface GmcAiGenerateDto {
  content: string;
  jsonLd?: string | null;
  contentType: string;
}

export interface GmcBulkAiItemDto {
  productEntityId: string;
  pageUrl: string;
  title?: string | null;
  result?: GmcAiGenerateDto | null;
  error?: string | null;
}

export interface GmcAiExplainDto {
  explanation: string;
  steps: string[];
}

export interface MerchantCenterAccountDto {
  accountId: string;
  name: string;
  websiteUrl?: string | null;
}

export interface MerchantCenterStatusDto {
  connected: boolean;
  accounts: MerchantCenterAccountDto[];
}

export interface GmcIntegrationStatusDto {
  integrations: GmcIntegrationItemDto[];
}

export interface GmcIntegrationItemDto {
  id: string;
  label: string;
  status: string;
  detail?: string | null;
  enabled: boolean;
}
