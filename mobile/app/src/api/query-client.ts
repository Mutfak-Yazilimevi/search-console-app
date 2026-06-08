import { QueryClient } from '@tanstack/react-query';
import { ApiError } from './api-client';

/**
 * Tek QueryClient instance — App.tsx'te QueryClientProvider ile sarmalanır.
 *
 * Retry stratejisi: 401/403/404 retry edilmez (anlamsız), network hatası 3x retry.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,        // 30sn boyunca stale sayma
        gcTime: 5 * 60_000,       // 5dk sonra GC
        retry: (failureCount, error) => {
          if (error instanceof ApiError) {
            // Auth/permission/not-found hataları retry edilmez
            if ([401, 403, 404].includes(error.status)) return false;
          }
          return failureCount < 3;
        },
        refetchOnReconnect: true,
      },
      mutations: {
        retry: false,
      },
    },
  });
}
