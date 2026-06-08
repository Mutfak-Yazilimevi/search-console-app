import {
  ProductComplianceDetailDto,
  ProductComplianceRunDto,
} from './merchant-center.models';

export function normalizeProductComplianceDetail(
  detail: ProductComplianceDetailDto,
): ProductComplianceDetailDto {
  return {
    ...detail,
    products: detail.products ?? [],
    siteIssues: detail.siteIssues ?? [],
    crossProductIssues: detail.crossProductIssues ?? [],
    feedIssues: detail.feedIssues ?? [],
    run: normalizeProductComplianceRun(detail.run),
  };
}

export function normalizeProductComplianceRun(run: ProductComplianceRunDto): ProductComplianceRunDto {
  return {
    ...run,
    priorityActions: run.priorityActions ?? [],
  };
}
