/**
 * TanStack Query hooks for Devices API
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { devicesApi } from '@/api/services'
import type {
  DeviceFilters,
  RegisterDeviceRequest,
  UpdateDeviceRequest,
} from '@/api/types'

const QUERY_KEY = 'devices'

/**
 * Get paginated list of devices
 */
export function useDevices(filters?: DeviceFilters) {
  return useQuery({
    queryKey: [QUERY_KEY, filters],
    queryFn: () => devicesApi.getDevices(filters),
    staleTime: 30_000, // 30 seconds
  })
}

/**
 * Get device by ID
 */
export function useDevice(id: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, id],
    queryFn: () => devicesApi.getDevice(id),
    enabled,
    staleTime: 30_000,
  })
}

/**
 * Register a new device
 */
export function useRegisterDevice() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: RegisterDeviceRequest) => devicesApi.registerDevice(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
    },
  })
}

/**
 * Update device
 */
export function useUpdateDevice() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateDeviceRequest }) =>
      devicesApi.updateDevice(id, data),
    onSuccess: (updatedDevice) => {
      // Update the specific device in cache
      queryClient.setQueryData([QUERY_KEY, updatedDevice.id], updatedDevice)
      // Invalidate list queries
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
    },
  })
}

/**
 * Delete device
 */
export function useDeleteDevice() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => devicesApi.deleteDevice(id),
    onSuccess: (_, deletedId) => {
      // Remove from cache
      queryClient.removeQueries({ queryKey: [QUERY_KEY, deletedId] })
      // Invalidate list queries
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
    },
  })
}

/**
 * Get device metrics (24h history)
 */
export function useDeviceMetrics(id: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, id, 'metrics'],
    queryFn: () => devicesApi.getDeviceMetrics(id),
    enabled,
    staleTime: 60_000, // 1 minute
    refetchInterval: 60_000, // Refetch every minute
  })
}

/**
 * Get device containers
 */
export function useDeviceContainers(id: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, id, 'containers'],
    queryFn: () => devicesApi.getDeviceContainers(id),
    enabled,
    staleTime: 30_000,
    refetchInterval: 30_000, // Refetch every 30 seconds
  })
}

/**
 * Get container logs
 */
export function useContainerLogs(
  deviceId: string,
  containerName: string,
  tail?: number,
  enabled = true
) {
  return useQuery({
    queryKey: [QUERY_KEY, deviceId, 'containers', containerName, 'logs', tail],
    queryFn: () => devicesApi.getContainerLogs(deviceId, containerName, tail),
    enabled,
    staleTime: 10_000,
    refetchInterval: 10_000, // Refetch every 10 seconds for logs
  })
}

/**
 * Get device activity/events
 */
export function useDeviceActivity(
  id: string,
  page: number = 1,
  pageSize: number = 20,
  enabled = true
) {
  return useQuery({
    queryKey: [QUERY_KEY, id, 'activity', page, pageSize],
    queryFn: () => devicesApi.getDeviceActivity(id, page, pageSize),
    enabled,
    staleTime: 30_000,
  })
}
