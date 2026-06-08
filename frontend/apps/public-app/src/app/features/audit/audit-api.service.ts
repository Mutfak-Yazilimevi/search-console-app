import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiClient } from '@SearchConsoleApp/shared/core';
import { AuditDetailDto, AuditRunDto, AltTextGenerationDto, BacklinkDto, ContentQualityDto, FaqGenerationDto, IndexStatusDto, KeywordDto, KeywordSerpDto, KeywordWatchDto, MetaGenerationDto, PageSpeedDto, PerformanceDto, SearchConsoleCoverageDto } from './audit.models';
import { APP_CONFIG } from '@SearchConsoleApp/shared/core';

export interface AuditDashboardSiteDto {
  normalizedUrl: string;
  label?: string | null;
  scheduleEntityId?: string | null;
  scheduleEnabled: boolean;
  latestScore?: number | null;
  latestCriticalCount: number;
  latestWarningCount: number;
  lastCompletedAt?: string | null;
  lastAuditEntityId?: string | null;
  scoreDelta?: number | null;
  totalRuns: number;
}

export interface ScheduledAuditDto {
  entityId: string;
  label?: string | null;
  url: string;
  searchConsolePropertyUrl?: string | null;
  migrationSourceUrl?: string | null;
  ga4PropertyId?: string | null;
  webhookUrl?: string | null;
  notifyOnComplete?: boolean;
  notifyOnCriticalOnly?: boolean;
  intervalDays: number;
  nextRunUtc: string;
  isEnabled: boolean;
}

export interface CreateScheduledAuditDto {
  url: string;
  label?: string;
  searchConsolePropertyUrl?: string;
  migrationSourceUrl?: string;
  ga4PropertyId?: string;
  webhookUrl?: string;
  notifyOnComplete?: boolean;
  notifyOnCriticalOnly?: boolean;
  intervalDays?: number;
}

@Injectable({ providedIn: 'root' })
export class AuditApiService {
  private api = inject(ApiClient);
  private config = inject(APP_CONFIG);

  start(url: string): Observable<AuditRunDto> {
    return this.api.post<AuditRunDto>('audit', { url });
  }

  startConnected(url: string, searchConsolePropertyUrl?: string): Observable<AuditRunDto> {
    return this.api.post<AuditRunDto>('audit', { url, searchConsolePropertyUrl }, { audience: 'web' });
  }

  get(entityId: string): Observable<AuditDetailDto> {
    return this.api.get<AuditDetailDto>(`audit/${entityId}`);
  }

  getPerformance(entityId: string): Observable<PerformanceDto | null> {
    return this.api.get<PerformanceDto | { available: false }>(`audit/${entityId}/performance`).pipe(
      map((r) => ('available' in r && r.available === false ? null : r as PerformanceDto)),
    );
  }

  getContentQuality(entityId: string): Observable<ContentQualityDto[]> {
    return this.api.get<ContentQualityDto[]>(`audit/${entityId}/content-quality`);
  }

  getPageSpeed(entityId: string): Observable<PageSpeedDto[]> {
    return this.api.get<PageSpeedDto[] | { available: false }>(`audit/${entityId}/pagespeed`).pipe(
      map((r) => ('available' in r && r.available === false ? [] : r as PageSpeedDto[])),
    );
  }

  getIndexStatus(entityId: string): Observable<IndexStatusDto | null> {
    return this.api.get<IndexStatusDto>(`audit/${entityId}/index-status`).pipe(
      map((r) => (r.available === false ? null : r)),
    );
  }

  getBacklinks(entityId: string): Observable<BacklinkDto | null> {
    return this.api.get<BacklinkDto>(`audit/${entityId}/backlinks`).pipe(
      map((r) => (r.available === false ? null : r)),
    );
  }

  getKeywords(entityId: string): Observable<KeywordDto[]> {
    return this.api.get<KeywordDto[] | { available: false }>(`audit/${entityId}/keywords`).pipe(
      map((r) => ('available' in r && r.available === false ? [] : r as KeywordDto[])),
    );
  }

  getSearchConsoleCoverage(entityId: string): Observable<SearchConsoleCoverageDto | null> {
    return this.api.get<SearchConsoleCoverageDto>(`audit/${entityId}/search-console-coverage`).pipe(
      map((r) => (r.available === false ? null : r)),
    );
  }

  exportJson(entityId: string): Observable<unknown> {
    return this.api.get(`audit/${entityId}/export`);
  }

  exportHtmlUrl(entityId: string): string {
    const base = this.config.apiRootUrl.replace(/\/$/, '');
    return `${base}/${this.config.defaultAudience}/audit/${entityId}/export?format=html`;
  }

  exportCriticalHtmlUrl(entityId: string): string {
    const base = this.config.apiRootUrl.replace(/\/$/, '');
    return `${base}/${this.config.defaultAudience}/audit/${entityId}/export?format=critical`;
  }

  eventsUrl(entityId: string): string {
    const base = this.config.apiRootUrl.replace(/\/$/, '');
    return `${base}/${this.config.defaultAudience}/audit/${entityId}/events`;
  }

  getGoogleLoginUrl(returnUrl: string): Observable<{ authorizeUrl: string }> {
    return this.api.get<{ authorizeUrl: string }>('auth/external/google', {
      params: { returnUrl },
    });
  }

  getDashboard(): Observable<AuditDashboardSiteDto[]> {
    return this.api.get<AuditDashboardSiteDto[]>('audit/dashboard', { audience: 'web' });
  }

  listSchedules(): Observable<ScheduledAuditDto[]> {
    return this.api.get<ScheduledAuditDto[]>('audit/schedules', { audience: 'web' });
  }

  createSchedule(body: CreateScheduledAuditDto): Observable<ScheduledAuditDto> {
    return this.api.post<ScheduledAuditDto>('audit/schedules', body, { audience: 'web' });
  }

  deleteSchedule(entityId: string): Observable<{ ok: boolean }> {
    return this.api.delete<{ ok: boolean }>(`audit/schedules/${entityId}`, { audience: 'web' });
  }

  listKeywordWatches(siteUrl?: string): Observable<KeywordWatchDto[]> {
    return this.api.get<KeywordWatchDto[]>('audit/keyword-watches', {
      audience: 'web',
      params: siteUrl ? { siteUrl } : undefined,
    });
  }

  createKeywordWatch(siteUrl: string, keyword: string): Observable<KeywordWatchDto> {
    return this.api.post<KeywordWatchDto>(
      'audit/keyword-watches',
      { siteUrl, keyword },
      { audience: 'web' },
    );
  }

  deleteKeywordWatch(entityId: string): Observable<{ ok: boolean }> {
    return this.api.delete<{ ok: boolean }>(`audit/keyword-watches/${entityId}`, { audience: 'web' });
  }

  getKeywordSerp(entityId: string): Observable<KeywordSerpDto[]> {
    return this.api.get<KeywordSerpDto[] | { available: false }>(`audit/${entityId}/keyword-serp`).pipe(
      map((r) => ('available' in r && r.available === false ? [] : r as KeywordSerpDto[])),
    );
  }

  cancel(entityId: string): Observable<AuditRunDto> {
    return this.api.post<AuditRunDto>(`audit/${entityId}/cancel`, {});
  }

  generateFaq(entityId: string, pageUrl: string): Observable<FaqGenerationDto> {
    return this.api.post<FaqGenerationDto>(`audit/${entityId}/generate-faq`, { pageUrl });
  }

  generateMeta(entityId: string, pageUrl: string, target: 'seo' | 'openGraph' = 'seo'): Observable<MetaGenerationDto> {
    return this.api.post<MetaGenerationDto>(`audit/${entityId}/generate-meta`, { pageUrl, target });
  }

  generateAltText(entityId: string, pageUrl: string, imageSrcs?: string[]): Observable<AltTextGenerationDto> {
    return this.api.post<AltTextGenerationDto>(`audit/${entityId}/generate-alt-text`, {
      pageUrl,
      imageSrcs: imageSrcs?.length ? imageSrcs : undefined,
    });
  }

  getIntegrationStatus(): Observable<{ integrations: IntegrationItemDto[] }> {
    return this.api.get<{ integrations: IntegrationItemDto[] }>('audit/integrations/status');
  }

  updateIntegration(
    integrationId: string,
    body: { enabled?: boolean; values?: Record<string, string> },
  ): Observable<IntegrationItemDto> {
    return this.api.patch<IntegrationItemDto>(`audit/integrations/${integrationId}`, body);
  }

  getRunIntegrations(entityId: string): Observable<RunIntegrationStatusDto> {
    return this.api.get<RunIntegrationStatusDto>(`audit/${entityId}/integrations`);
  }

  updateSchedule(entityId: string, body: Partial<CreateScheduledAuditDto & { isEnabled?: boolean }>): Observable<ScheduledAuditDto> {
    return this.api.patch<ScheduledAuditDto>(`audit/schedules/${entityId}`, body, { audience: 'web' });
  }
}

export interface IntegrationFieldDto {
  key: string;
  label: string;
  isSecret: boolean;
  hasValue: boolean;
  maskedValue?: string | null;
}

export interface IntegrationItemDto {
  id: string;
  label: string;
  status: string;
  detail?: string | null;
  configKey?: string | null;
  enabled?: boolean;
  canToggle?: boolean;
  fields?: IntegrationFieldDto[];
}

export interface RunIntegrationStatusDto {
  auditRunEntityId: string;
  mode: string;
  steps: IntegrationItemDto[];
}
