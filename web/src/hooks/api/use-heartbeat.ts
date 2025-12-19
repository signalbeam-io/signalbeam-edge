/**
 * TanStack Query hooks for Heartbeat API
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { heartbeatApi } from '@/api/services'
import type { HeartbeatFilters, SendHeartbeatRequest } from '@/api/types'

const QUERY_KEY = 'heartbeat'

/**
 * Get device heartbeat history
 */
export function useHeartbeats(filters: HeartbeatFilters) {
  return useQuery({
    queryKey: [QUERY_KEY, filters],
    queryFn: () => heartbeatApi.getHeartbeats(filters),
    staleTime: 10_000, // 10 seconds
  })
}

/**
 * Get latest heartbeat for a device
 */
export function useLatestHeartbeat(deviceId: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, 'latest', deviceId],
    queryFn: () => heartbeatApi.getLatestHeartbeat(deviceId),
    enabled,
    staleTime: 10_000,
    refetchInterval: 30_000, // Refetch every 30 seconds
  })
}

/**
 * Send heartbeat (used by edge agent)
 */
export function useSendHeartbeat() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: SendHeartbeatRequest) => heartbeatApi.sendHeartbeat(data),
    onSuccess: (_, variables) => {
      // Invalidate heartbeat queries for this device
      queryClient.invalidateQueries({
        queryKey: [QUERY_KEY, 'latest', variables.deviceId],
      })
      queryClient.invalidateQueries({
        predicate: (query) => {
          const key = query.queryKey
          return (
            Array.isArray(key) &&
            key[0] === QUERY_KEY &&
            typeof key[1] === 'object' &&
            key[1] !== null &&
            'deviceId' in key[1] &&
            key[1].deviceId === variables.deviceId
          )
        },
      })
    },
  })
}
