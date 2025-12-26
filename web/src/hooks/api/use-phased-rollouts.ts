/**
 * TanStack Query hooks for Phased Rollouts API
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { rolloutsApi } from '@/api/services'
import type { PhasedRolloutFilters, CreatePhasedRolloutRequest } from '@/api/types'

const QUERY_KEY = 'phased-rollouts'

/**
 * Get paginated list of phased rollouts
 */
export function usePhasedRollouts(filters?: PhasedRolloutFilters) {
  return useQuery({
    queryKey: [QUERY_KEY, filters],
    queryFn: () => rolloutsApi.getPhasedRollouts(filters),
    staleTime: 20_000, // 20 seconds
  })
}

/**
 * Get phased rollout by ID
 */
export function usePhasedRollout(id: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, id],
    queryFn: () => rolloutsApi.getPhasedRollout(id),
    enabled,
    staleTime: 10_000, // 10 seconds
    refetchInterval: (query) => {
      // Auto-refetch every 5 seconds if rollout is in progress
      const data = query.state.data
      if (data && (data.status === 'InProgress' || data.status === 'Pending')) {
        return 5_000
      }
      return false
    },
  })
}

/**
 * Get active rollouts
 */
export function useActiveRollouts() {
  return useQuery({
    queryKey: [QUERY_KEY, 'active'],
    queryFn: () => rolloutsApi.getActiveRollouts(),
    staleTime: 15_000, // 15 seconds
    refetchInterval: 10_000, // Refresh every 10 seconds for active rollouts
  })
}

/**
 * Create a new phased rollout
 */
export function useCreatePhasedRollout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: CreatePhasedRolloutRequest) => rolloutsApi.createPhasedRollout(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
      // Also invalidate bundles as their rollout status may change
      queryClient.invalidateQueries({ queryKey: ['bundles'] })
    },
  })
}

/**
 * Start a phased rollout
 */
export function useStartPhasedRollout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => rolloutsApi.startPhasedRollout(id),
    onSuccess: (updatedRollout) => {
      queryClient.setQueryData([QUERY_KEY, updatedRollout.id], updatedRollout)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY, 'active'] })
    },
  })
}

/**
 * Pause a phased rollout
 */
export function usePausePhasedRollout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => rolloutsApi.pausePhasedRollout(id),
    onSuccess: (updatedRollout) => {
      queryClient.setQueryData([QUERY_KEY, updatedRollout.id], updatedRollout)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
    },
  })
}

/**
 * Resume a paused phased rollout
 */
export function useResumePhasedRollout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => rolloutsApi.resumePhasedRollout(id),
    onSuccess: (updatedRollout) => {
      queryClient.setQueryData([QUERY_KEY, updatedRollout.id], updatedRollout)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY, 'active'] })
    },
  })
}

/**
 * Rollback a phased rollout to previous version
 */
export function useRollbackPhasedRollout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => rolloutsApi.rollbackPhasedRollout(id),
    onSuccess: (updatedRollout) => {
      queryClient.setQueryData([QUERY_KEY, updatedRollout.id], updatedRollout)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
      // Also invalidate devices as they may be rolling back
      queryClient.invalidateQueries({ queryKey: ['devices'] })
    },
  })
}
