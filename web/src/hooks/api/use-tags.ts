/**
 * TanStack Query hooks for Tags API
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { tagsApi } from '@/api/services'
import type {
  AddDeviceTagRequest,
  RemoveDeviceTagRequest,
  SearchDevicesByTagQueryRequest,
  BulkAddTagsRequest,
  BulkRemoveTagsRequest,
} from '@/api/types'

const QUERY_KEY = 'tags'
const DEVICES_QUERY_KEY = 'devices'

/**
 * Get all tags with usage counts
 */
export function useTags() {
  return useQuery({
    queryKey: [QUERY_KEY],
    queryFn: () => tagsApi.getAllTags(),
    staleTime: 60_000, // 1 minute
  })
}

/**
 * Add tag to a device
 */
export function useAddDeviceTag() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: AddDeviceTagRequest) => tagsApi.addDeviceTag(request),
    onSuccess: (_, variables) => {
      // Invalidate device cache to refetch with new tags
      queryClient.invalidateQueries({ queryKey: [DEVICES_QUERY_KEY, variables.deviceId] })
      queryClient.invalidateQueries({ queryKey: [DEVICES_QUERY_KEY], exact: false })
      // Invalidate tags list
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
    },
  })
}

/**
 * Remove tag from a device
 */
export function useRemoveDeviceTag() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: RemoveDeviceTagRequest) => tagsApi.removeDeviceTag(request),
    onSuccess: (_, variables) => {
      // Invalidate device cache
      queryClient.invalidateQueries({ queryKey: [DEVICES_QUERY_KEY, variables.deviceId] })
      queryClient.invalidateQueries({ queryKey: [DEVICES_QUERY_KEY], exact: false })
      // Invalidate tags list
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
    },
  })
}

/**
 * Search devices by tag query expression
 * Example queries: "environment=production AND location=warehouse-*"
 */
export function useSearchDevicesByTagQuery(request: SearchDevicesByTagQueryRequest) {
  return useQuery({
    queryKey: [QUERY_KEY, 'search', request],
    queryFn: () => tagsApi.searchDevicesByTagQuery(request),
    enabled: !!request.query, // Only run if query is provided
    staleTime: 30_000, // 30 seconds
  })
}

/**
 * Bulk add tag to all devices in a group
 */
export function useBulkAddTags() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: BulkAddTagsRequest) => tagsApi.bulkAddTags(request),
    onSuccess: () => {
      // Invalidate all device queries since multiple devices were updated
      queryClient.invalidateQueries({ queryKey: [DEVICES_QUERY_KEY] })
      // Invalidate tags list
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
      // Invalidate groups since memberships might have changed for dynamic groups
      queryClient.invalidateQueries({ queryKey: ['groups'] })
    },
  })
}

/**
 * Bulk remove tag from all devices in a group
 */
export function useBulkRemoveTags() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: BulkRemoveTagsRequest) => tagsApi.bulkRemoveTags(request),
    onSuccess: () => {
      // Invalidate all device queries since multiple devices were updated
      queryClient.invalidateQueries({ queryKey: [DEVICES_QUERY_KEY] })
      // Invalidate tags list
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
      // Invalidate groups since memberships might have changed for dynamic groups
      queryClient.invalidateQueries({ queryKey: ['groups'] })
    },
  })
}
