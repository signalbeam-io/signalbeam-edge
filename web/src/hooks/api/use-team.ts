/**
 * TanStack Query hooks for Team API
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { teamApi } from '@/api/services/team.api'
import type {
  TeamMembersFilters,
  InviteUserRequest,
  ChangeRoleRequest,
} from '@/api/types'

const MEMBERS_KEY = 'teamMembers'
const INVITATIONS_KEY = 'teamInvitations'

/**
 * Get paginated list of team members
 */
export function useTeamMembers(filters?: TeamMembersFilters) {
  return useQuery({
    queryKey: [MEMBERS_KEY, filters],
    queryFn: () => teamApi.getMembers(filters),
    staleTime: 60_000,
  })
}

/**
 * Get pending invitations
 */
export function usePendingInvitations() {
  return useQuery({
    queryKey: [INVITATIONS_KEY],
    queryFn: () => teamApi.getPendingInvitations(),
    staleTime: 60_000,
  })
}

/**
 * Invite a user to the team
 */
export function useInviteUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (data: InviteUserRequest) => teamApi.inviteUser(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [INVITATIONS_KEY] })
    },
  })
}

/**
 * Change a team member's role
 */
export function useChangeRole() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ userId, data }: { userId: string; data: ChangeRoleRequest }) =>
      teamApi.changeRole(userId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [MEMBERS_KEY] })
    },
  })
}

/**
 * Remove a team member
 */
export function useRemoveUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (userId: string) => teamApi.removeMember(userId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [MEMBERS_KEY] })
    },
  })
}
