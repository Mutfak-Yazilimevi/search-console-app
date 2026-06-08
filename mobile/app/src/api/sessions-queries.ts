import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useApiClient } from './api-client-provider';

export interface SessionDto {
  id: number;
  deviceId: number;
  audience: string;
  ipAddress?: string;
  ipCountry?: string;
  userAgent?: string;
  startedUtc: string;
  lastActivityUtc: string;
  isCurrent: boolean;
  isActive: boolean;
  revokedUtc?: string;
  revokedReason?: string;
}

/** Aktif oturumlar — IsCurrent JWT'deki session ID ile match'leşir. */
export function useActiveSessions() {
  const client = useApiClient();
  return useQuery({
    queryKey: ['web', 'sessions'],
    queryFn: () => client.get<SessionDto[]>('web', 'sessions'),
  });
}

export function useRevokeSession() {
  const client = useApiClient();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (sessionId: number) => client.post('web', `sessions/${sessionId}/revoke`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['web', 'sessions'] }),
  });
}

export function useRevokeOtherSessions() {
  const client = useApiClient();
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => client.post('web', 'sessions/revoke-others'),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['web', 'sessions'] }),
  });
}
