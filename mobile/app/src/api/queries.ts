import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useApiClient } from './api-client-provider';
import type { Customer } from '@/models';

/**
 * Hook patterns:
 * - useXxx → query
 * - useXxxMutation → mutation
 *
 * Audience hook ismine yansır → useAdminCustomers vs useWebMe gibi.
 * Tek bir client tüm audience'lara konuşur, ama hook'lar net.
 */

// === WEB audience ===

export function useWebMe() {
  const client = useApiClient();
  return useQuery({
    queryKey: ['web', 'me'],
    queryFn: () => client.get<Customer>('web', 'me'),
  });
}

// === ADMIN audience ===

export function useAdminCustomers(onlyActive = true) {
  const client = useApiClient();
  return useQuery({
    queryKey: ['admin', 'customers', { onlyActive }],
    queryFn: () => client.get<Customer[]>('admin', 'customers', { onlyActive }),
  });
}

export function useAdminCustomer(entityId: string | null) {
  const client = useApiClient();
  return useQuery({
    queryKey: ['admin', 'customers', entityId],
    queryFn: () => client.get<Customer>('admin', `customers/${entityId}`),
    enabled: !!entityId,
  });
}

export function useDeleteAdminCustomer() {
  const client = useApiClient();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (entityId: string) => client.delete('admin', `customers/${entityId}`),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['admin', 'customers'] });
    },
  });
}
