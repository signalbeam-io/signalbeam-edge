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
