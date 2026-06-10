import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@SearchConsoleApp/shared/core';
import {
  PriceBenchmarkDetailDto,
  PriceBenchmarkItemDto,
  PriceBenchmarkRunDto,
} from './price-benchmark.models';

@Injectable({ providedIn: 'root' })
export class PriceBenchmarkApiService {
  private api = inject(ApiClient);

  start(url: string): Observable<PriceBenchmarkRunDto> {
    return this.api.post<PriceBenchmarkRunDto>('price-benchmark', { url });
  }

  get(entityId: string): Observable<PriceBenchmarkDetailDto> {
    return this.api.get<PriceBenchmarkDetailDto>(`price-benchmark/${entityId}`);
  }

  getProducts(entityId: string, skip = 0, take = 50): Observable<PriceBenchmarkItemDto[]> {
    return this.api.get<PriceBenchmarkItemDto[]>(`price-benchmark/${entityId}/products`, {
      params: { skip, take },
    });
  }

  cancel(entityId: string): Observable<PriceBenchmarkRunDto> {
    return this.api.post<PriceBenchmarkRunDto>(`price-benchmark/${entityId}/cancel`, {});
  }
}
