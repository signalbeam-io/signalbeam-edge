/**
 * Team management API client
 */

import { apiRequest } from '../client'
import { appendTenantId } from './tenant'
import type {
  TeamMembersFilters,
  PaginatedTeamMembers,
  Invitation,
  InviteUserRequest,
  InviteUserResponse,
  ChangeRoleRequest,
  AcceptInvitationResponse,
} from '../types'

const BASE_PATH = '/api/teams'

export const teamApi = {
  async getMembers(filters?: TeamMembersFilters): Promise<PaginatedTeamMembers> {
    const params = new URLSearchParams()
    appendTenantId(params)
    if (filters?.page) params.append('page', filters.page.toString())
    if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString())
    if (filters?.search) params.append('search', filters.search)
    return apiRequest<PaginatedTeamMembers>({
      method: 'GET',
      url: `${BASE_PATH}/members?${params.toString()}`,
    })
  },

  async inviteUser(data: InviteUserRequest): Promise<InviteUserResponse> {
    return apiRequest<InviteUserResponse>({
      method: 'POST',
      url: `${BASE_PATH}/invite`,
      data,
    })
  },

  async changeRole(userId: string, data: ChangeRoleRequest): Promise<void> {
    await apiRequest<void>({
      method: 'PUT',
      url: `${BASE_PATH}/members/${userId}/role`,
      data,
    })
  },

  async removeMember(userId: string): Promise<void> {
    await apiRequest<void>({
      method: 'DELETE',
      url: `${BASE_PATH}/members/${userId}`,
    })
  },

  async getPendingInvitations(): Promise<Invitation[]> {
    return apiRequest<Invitation[]>({
      method: 'GET',
      url: `${BASE_PATH}/invitations`,
    })
  },

  async acceptInvitation(token: string): Promise<AcceptInvitationResponse> {
    return apiRequest<AcceptInvitationResponse>({
      method: 'POST',
      url: `${BASE_PATH}/invitations/${token}/accept`,
    })
  },

  async declineInvitation(token: string): Promise<void> {
    await apiRequest<void>({
      method: 'POST',
      url: `${BASE_PATH}/invitations/${token}/decline`,
    })
  },
}
