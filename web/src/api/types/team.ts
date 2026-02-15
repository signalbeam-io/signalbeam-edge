/**
 * Team management types
 */

import type { PaginatedResponse, PaginationParams } from './common'
import type { UserRole } from '@/stores/auth-store'

export type InvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Expired'

export interface TeamMember {
  userId: string
  email: string
  name: string
  role: UserRole
  status: string
  createdAt: string
  lastLoginAt: string | null
}

export interface Invitation {
  invitationId: string
  email: string
  role: UserRole
  status: InvitationStatus
  createdAt: string
  expiresAt: string
  isExpired: boolean
}

export interface InviteUserRequest {
  email: string
  role: UserRole
}

export interface InviteUserResponse {
  invitationId: string
  email: string
  role: string
  expiresAt: string
}

export interface ChangeRoleRequest {
  role: UserRole
}

export interface AcceptInvitationResponse {
  tenantId: string
  email: string
  role: string
}

export interface TeamMembersFilters extends PaginationParams {
  search?: string | undefined
}

export type PaginatedTeamMembers = PaginatedResponse<TeamMember>
