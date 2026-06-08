import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../api-client.service';

export interface AuditLogDto {
  id: number;
  timestamp: string;
  audience: string;
  actorCustomerId?: number;
  actorEmail?: string;
  actorIp?: string;
  actorUserAgent?: string;
  action: string;
  targetType?: string;
  targetId?: number;
  targetEntityId?: string;
  outcome: string;
  failureReason?: string;
  changesJson?: string;
  correlationId?: string;
}

export interface AuditQueryParams {
  actorCustomerId?: number;
  targetType?: string;
  targetId?: number;
  audience?: string;
  action?: string;
  from?: string;
  to?: string;
  take?: number;
  skip?: number;
}

@Injectable({ providedIn: 'root' })
export class AdminAuditService {
  private api = inject(ApiClient);

  query(params: AuditQueryParams = {}): Observable<AuditLogDto[]> {
    const queryParams: Record<string, string | number | boolean> = {};
    Object.entries(params).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') queryParams[k] = v as string | number | boolean;
    });
    return this.api.get<AuditLogDto[]>('audit', { audience: 'admin', params: queryParams });
  }

  byCustomer(customerId: number, take = 100): Observable<AuditLogDto[]> {
    return this.api.get<AuditLogDto[]>(`audit/customers/${customerId}`, {
      audience: 'admin',
      params: { take }
    });
  }

  byEntity(targetType: string, targetId: number, take = 100): Observable<AuditLogDto[]> {
    return this.api.get<AuditLogDto[]>(`audit/entity/${targetType}/${targetId}`, {
      audience: 'admin',
      params: { take }
    });
  }
}
