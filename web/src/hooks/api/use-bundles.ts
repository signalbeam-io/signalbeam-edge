/**
 * TanStack Query hooks for Bundles API
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { bundlesApi } from '@/api/services'
import type {
  BundleFilters,
  CreateBundleRequest,
  UpdateBundleRequest,
  CreateBundleVersionRequest,
} from '@/api/types'

const QUERY_KEY = 'bundles'

/**
 * Get paginated list of bundles
 */
export function useBundles(filters?: BundleFilters) {
  return useQuery({
    queryKey: [QUERY_KEY, filters],
    queryFn: () => bundlesApi.getBundles(filters),
    staleTime: 60_000, // 1 minute
  })
}

/**
 * Get bundle by ID
 */
export function useBundle(id: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, id],
    queryFn: () => bundlesApi.getBundle(id),
    enabled,
    staleTime: 60_000,
  })
}

/**
 * Create a new bundle
 */
export function useCreateBundle() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: CreateBundleRequest) => bundlesApi.createBundle(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
    },
  })
}

/**
 * Update bundle
 */
export function useUpdateBundle() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateBundleRequest }) =>
      bundlesApi.updateBundle(id, data),
    onSuccess: (updatedBundle) => {
      queryClient.setQueryData([QUERY_KEY, updatedBundle.id], updatedBundle)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
    },
  })
}

/**
 * Delete bundle
 */
export function useDeleteBundle() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => bundlesApi.deleteBundle(id),
    onSuccess: (_, deletedId) => {
      queryClient.removeQueries({ queryKey: [QUERY_KEY, deletedId] })
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
    },
  })
}

/**
 * Get bundle versions
 */
export function useBundleVersions(bundleId: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, bundleId, 'versions'],
    queryFn: () => bundlesApi.getBundleVersions(bundleId),
    enabled,
    staleTime: 60_000,
  })
}

/**
 * Create a new bundle version
 */
export function useCreateBundleVersion() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ bundleId, data }: { bundleId: string; data: CreateBundleVersionRequest }) =>
      bundlesApi.createBundleVersion(bundleId, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY, variables.bundleId, 'versions'] })
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY, variables.bundleId] })
    },
  })
}

/**
 * Set active version
 */
export function useSetActiveVersion() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ bundleId, version }: { bundleId: string; version: string }) =>
      bundlesApi.setActiveVersion(bundleId, version),
    onSuccess: (updatedBundle) => {
      queryClient.setQueryData([QUERY_KEY, updatedBundle.id], updatedBundle)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY, updatedBundle.id, 'versions'] })
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
    },
  })
}
