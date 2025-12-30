/**
 * TanStack Query hooks for Alerts API
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { alertsApi } from '@/api/services'
import type { AlertFilters, AcknowledgeAlertRequest } from '@/api/types'

const QUERY_KEY = 'alerts'

/**
 * Get list of alerts with filtering
 */
export function useAlerts(filters?: AlertFilters) {
  return useQuery({
    queryKey: [QUERY_KEY, filters],
    queryFn: () => alertsApi.getAlerts(filters),
    staleTime: 30_000, // 30 seconds
    refetchInterval: 60_000, // Refetch every minute for fresh alerts
  })
}

/**
 * Get alert by ID with notifications
 */
export function useAlert(id: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, id],
    queryFn: () => alertsApi.getAlertById(id),
    enabled,
    staleTime: 30_000,
  })
}

/**
 * Get alert statistics
 */
export function useAlertStatistics(tenantId?: string) {
  return useQuery({
    queryKey: [QUERY_KEY, 'statistics', tenantId],
    queryFn: () => alertsApi.getStatistics(tenantId),
    staleTime: 60_000, // 1 minute
    refetchInterval: 60_000, // Refetch every minute
  })
}

/**
 * Acknowledge an alert
 */
export function useAcknowledgeAlert() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: AcknowledgeAlertRequest }) =>
      alertsApi.acknowledgeAlert(id, request),
    onSuccess: () => {
      // Invalidate alert list and statistics
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
    },
  })
}

/**
 * Resolve an alert
 */
export function useResolveAlert() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => alertsApi.resolveAlert(id),
    onSuccess: () => {
      // Invalidate alert list and statistics
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
    },
  })
}
