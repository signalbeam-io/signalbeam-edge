/**
 * TanStack Query hooks for Groups API
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { groupsApi } from '@/api/services'
import type { GroupFilters, CreateGroupRequest, UpdateGroupRequest } from '@/api/types'

const QUERY_KEY = 'groups'

/**
 * Get paginated list of groups
 */
export function useGroups(filters?: GroupFilters) {
  return useQuery({
    queryKey: [QUERY_KEY, filters],
    queryFn: () => groupsApi.getGroups(filters),
    staleTime: 60_000, // 1 minute
  })
}

/**
 * Get group by ID
 */
export function useGroup(id: string, enabled = true) {
  return useQuery({
    queryKey: [QUERY_KEY, id],
    queryFn: () => groupsApi.getGroup(id),
    enabled,
    staleTime: 60_000,
  })
}

/**
 * Create a new group
 */
export function useCreateGroup() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: CreateGroupRequest) => groupsApi.createGroup(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY] })
    },
  })
}

/**
 * Update group
 */
export function useUpdateGroup() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateGroupRequest }) =>
      groupsApi.updateGroup(id, data),
    onSuccess: (updatedGroup) => {
      queryClient.setQueryData([QUERY_KEY, updatedGroup.id], updatedGroup)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
    },
  })
}

/**
 * Delete group
 */
export function useDeleteGroup() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => groupsApi.deleteGroup(id),
    onSuccess: (_, deletedId) => {
      queryClient.removeQueries({ queryKey: [QUERY_KEY, deletedId] })
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
    },
  })
}

/**
 * Add devices to group
 */
export function useAddDevicesToGroup() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ groupId, deviceIds }: { groupId: string; deviceIds: string[] }) =>
      groupsApi.addDevicesToGroup(groupId, deviceIds),
    onSuccess: (updatedGroup) => {
      queryClient.setQueryData([QUERY_KEY, updatedGroup.id], updatedGroup)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
      // Also invalidate devices as their group membership changed
      queryClient.invalidateQueries({ queryKey: ['devices'] })
    },
  })
}

/**
 * Remove devices from group
 */
export function useRemoveDevicesFromGroup() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ groupId, deviceIds }: { groupId: string; deviceIds: string[] }) =>
      groupsApi.removeDevicesFromGroup(groupId, deviceIds),
    onSuccess: (updatedGroup) => {
      queryClient.setQueryData([QUERY_KEY, updatedGroup.id], updatedGroup)
      queryClient.invalidateQueries({ queryKey: [QUERY_KEY], exact: false })
      queryClient.invalidateQueries({ queryKey: ['devices'] })
    },
  })
}
