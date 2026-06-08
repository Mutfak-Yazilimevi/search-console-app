import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiClient, APP_CONFIG } from '@SearchConsoleApp/shared/core';
import {
  GmcAiExplainDto,
  GmcAiGenerateDto,
  GmcAiSummaryDto,
  GmcBulkAiItemDto,
  GmcIntegrationStatusDto,
  ProductComplianceDetailDto,
  ProductComplianceItemDto,
  ProductComplianceProductDetailDto,
  ProductComplianceRunDto,
  ProductComplianceRunSummaryDto,
} from './merchant-center.models';
import { normalizeProductComplianceDetail } from './product-compliance-normalize';

@Injectable({ providedIn: 'root' })
export class ProductComplianceApiService {
  private api = inject(ApiClient);
  private config = inject(APP_CONFIG);

  start(url: string): Observable<ProductComplianceRunDto> {
    return this.api.post<ProductComplianceRunDto>('merchant-center/compliance', { url });
  }

  startConnected(url: string, merchantCenterAccountId?: string): Observable<ProductComplianceRunDto> {
    return this.api.post<ProductComplianceRunDto>(
      'merchant-center/compliance',
      { url, merchantCenterAccountId },
      { audience: 'web' },
    );
  }

  get(entityId: string): Observable<ProductComplianceDetailDto> {
    return this.api
      .get<ProductComplianceDetailDto>(`merchant-center/compliance/${entityId}`)
      .pipe(map(normalizeProductComplianceDetail));
  }

  getIntegrationStatus(): Observable<GmcIntegrationStatusDto> {
    return this.api.get<GmcIntegrationStatusDto>('merchant-center/compliance/integrations/status');
  }

  listRecentRuns(limit = 10): Observable<ProductComplianceRunSummaryDto[]> {
    return this.api.get<ProductComplianceRunSummaryDto[]>('merchant-center/compliance/runs', {
      audience: 'web',
      params: { limit },
    });
  }

  getProducts(entityId: string, skip = 0, take = 50): Observable<ProductComplianceItemDto[]> {
    return this.api.get<ProductComplianceItemDto[]>(`merchant-center/compliance/${entityId}/products`, {
      params: { skip, take },
    });
  }

  getProduct(entityId: string, productId: string): Observable<ProductComplianceProductDetailDto> {
    return this.api.get<ProductComplianceProductDetailDto>(
      `merchant-center/compliance/${entityId}/products/${productId}`,
    );
  }

  exportJson(entityId: string): Observable<ProductComplianceDetailDto> {
    return this.api
      .get<ProductComplianceDetailDto>(`merchant-center/compliance/${entityId}/export`)
      .pipe(map(normalizeProductComplianceDetail));
  }

  exportHtmlUrl(entityId: string): string {
    const base = this.config.apiRootUrl.replace(/\/$/, '');
    return `${base}/${this.config.defaultAudience}/merchant-center/compliance/${entityId}/export?format=html`;
  }

  cancel(entityId: string): Observable<{ ok: boolean }> {
    return this.api.post<{ ok: boolean }>(`merchant-center/compliance/${entityId}/cancel`, {});
  }

  aiSummary(entityId: string): Observable<GmcAiSummaryDto> {
    return this.api.post<GmcAiSummaryDto>(`merchant-center/compliance/${entityId}/ai/summary`, {});
  }

  aiGenerateProduct(entityId: string, productId: string, type: string): Observable<GmcAiGenerateDto> {
    return this.api.post<GmcAiGenerateDto>(
      `merchant-center/compliance/${entityId}/products/${productId}/ai/generate`,
      { type },
    );
  }

  aiGenerateSite(entityId: string, type: string): Observable<GmcAiGenerateDto> {
    return this.api.post<GmcAiGenerateDto>(
      `merchant-center/compliance/${entityId}/ai/site/generate`,
      { type },
    );
  }

  aiExplainIssue(entityId: string, issueId: string): Observable<GmcAiExplainDto> {
    return this.api.post<GmcAiExplainDto>(
      `merchant-center/compliance/${entityId}/ai/issues/${issueId}/explain`,
      {},
    );
  }

  rescanProduct(entityId: string, productId: string): Observable<{ ok: boolean }> {
    return this.api.post<{ ok: boolean }>(
      `merchant-center/compliance/${entityId}/products/${productId}/rescan`,
      {},
    );
  }

  aiBulkGenerate(entityId: string, type: string): Observable<GmcBulkAiItemDto[]> {
    return this.api.post<GmcBulkAiItemDto[]>(
      `merchant-center/compliance/${entityId}/ai/bulk-generate`,
      { type },
    );
  }
}
