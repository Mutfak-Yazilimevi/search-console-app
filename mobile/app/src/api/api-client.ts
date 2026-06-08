import type { AppConfig } from '@/config/app-config';
import type { SecureStorage } from '@/utils/secure-storage';
import type { ApiResponse, Audience, ProblemDetails } from '@/models';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: ProblemDetails,
  ) {
    super(problem.title);
  }
}

export interface ApiClientOptions {
  config: AppConfig;
  storage: SecureStorage;
  onUnauthorized?: () => void;
}

/**
 * Fetch-based HTTP client. Axios YOK — RN 0.85+ fetch performansı yeterli,
 * paket boyutu ~50KB azalıyor.
 *
 * Audience-aware: aynı client `/api/public/*`, `/api/web/*`, `/api/admin/*`
 * adreslerine istek atabilir. Her metod ilk argüman olarak audience alır.
 *
 * Backend ApiResponse<T> zarfını otomatik unwrap eder.
 * Hata durumunda ApiError fırlatır (ProblemDetails ile).
 */
export class ApiClient {
  constructor(private readonly opts: ApiClientOptions) {}

  get<T>(audience: Audience, path: string, params?: Record<string, unknown>) {
    return this.request<T>(audience, 'GET', path, undefined, params);
  }
  post<T>(audience: Audience, path: string, body?: unknown) {
    return this.request<T>(audience, 'POST', path, body);
  }
  put<T>(audience: Audience, path: string, body?: unknown) {
    return this.request<T>(audience, 'PUT', path, body);
  }
  patch<T>(audience: Audience, path: string, body?: unknown) {
    return this.request<T>(audience, 'PATCH', path, body);
  }
  delete<T = void>(audience: Audience, path: string) {
    return this.request<T>(audience, 'DELETE', path);
  }

  private async request<T>(
    audience: Audience,
    method: string,
    path: string,
    body?: unknown,
    params?: Record<string, unknown>,
  ): Promise<T> {
    const url = this.buildUrl(audience, path, params);
    const token = await this.opts.storage.getItem(this.opts.config.tokenStorageKey);

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      Accept: 'application/json',
    };
    if (token && audience !== 'public') {
      headers.Authorization = `Bearer ${token}`;
    }

    let res: Response;
    try {
      res = await fetch(url, {
        method,
        headers,
        body: body !== undefined ? JSON.stringify(body) : undefined,
      });
    } catch (err) {
      // Network error — backend'e ulaşılamadı
      throw new ApiError(0, {
        title: 'Network error',
        status: 0,
        detail: err instanceof Error ? err.message : String(err),
      });
    }

    if (res.status === 401 && audience !== 'public') {
      this.opts.onUnauthorized?.();
    }

    if (!res.ok) {
      let problem: ProblemDetails;
      try {
        problem = await res.json();
      } catch {
        problem = { title: res.statusText, status: res.status };
      }
      throw new ApiError(res.status, problem);
    }

    // 204 No Content
    if (res.status === 204) return undefined as T;

    const json = (await res.json()) as ApiResponse<T>;
    return json.data;
  }

  private buildUrl(audience: Audience, path: string, params?: Record<string, unknown>): string {
    const base = this.opts.config.apiBaseUrl.replace(/\/$/, '');
    const cleanPath = path.replace(/^\//, '');
    let url = `${base}/${audience}/${cleanPath}`;

    if (params && Object.keys(params).length > 0) {
      const qs = new URLSearchParams();
      for (const [k, v] of Object.entries(params)) {
        if (v !== undefined && v !== null) qs.append(k, String(v));
      }
      url += `?${qs.toString()}`;
    }
    return url;
  }
}
