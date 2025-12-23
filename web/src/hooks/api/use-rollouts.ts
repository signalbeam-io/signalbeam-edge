/**
 * TanStack Query hooks for Rollouts API
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { rolloutsApi } from '@/api/services'
import type { RolloutFilters, CreateRolloutRequest } from '@/api/types'

const QUERY_KEY = 'rollouts'

/**
 * Get paginated list of rollouts
 */
export function useRollouts(filters?: RolloutFilters) {
  return useQuery({
    queryKey: [QUERY_KEY, filters],
    queryFn: () => rolloutsApi.getRollouts(filters),
    staleTime: 20_000, // 20 seconds
    retry: (failureCount, error: unknown) => {
      // Don't retry if the endpoint doesn't exist (405 Method Not Allowed)
      const apiError = error as { response?: { status?: number } }
      if (apiError?.response?.status === 405) {
        return false
      }
      return failureCount < 3
    },
  })
}

/**
 * Get rollout by ID
 */
export function useRollout(id: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, id],
    queryFn: () => rolloutsApi.getRollout(id),
    enabled,
    staleTime: 10_000, // 10 seconds for active rollouts
    refetchInterval: (query) => {
      // Auto-refetch every 5 seconds if rollout is in progress
      const data = query.state.data
      if (data && (data.status === 'pending' || data.status === 'in_progress')) {
        return 5_000
      }
      return false
    },
  })
}

/**
 * Create a new rollout
 */
export function useCreateRollout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: CreateRolloutRequest) => rolloutsApi.createRollout(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
      // Also invalidate devices as their status may change
      queryClient.invalidateQueries({ queryKey: ['devices'] })
    },
  })
}

/**
 * Cancel rollout
 */
export function useCancelRollout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => rolloutsApi.cancelRollout(id),
    onSuccess: (updatedRollout) => {
      queryClient.setQueryData([QUERY_KEY, updatedRollout.id], updatedRollout)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
      queryClient.invalidateQueries({ queryKey: ['devices'] })
    },
  })
}

/**
 * Get device-level rollout status
 */
export function useDeviceRolloutStatus(rolloutId: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, rolloutId, 'devices'],
    queryFn: () => rolloutsApi.getDeviceRolloutStatus(rolloutId),
    enabled,
    staleTime: 10_000,
    refetchInterval: 10_000, // Auto-refresh every 10 seconds
  })
}

/**
 * Retry failed devices in a rollout
 */
export function useRetryFailedDevices() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (rolloutId: string) => rolloutsApi.retryFailedDevices(rolloutId),
    onSuccess: (updatedRollout) => {
      queryClient.setQueryData([QUERY_KEY, updatedRollout.id], updatedRollout)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY, updatedRollout.id, 'devices'] })
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
    },
  })
}
