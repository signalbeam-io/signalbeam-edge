/**
 * Device Group types
 */

import type { PaginationParams } from './common'

export enum GroupType {
  Static = 'Static',
  Dynamic = 'Dynamic',
}

export enum MembershipType {
  Static = 'Static',
  Dynamic = 'Dynamic',
}

export interface DeviceGroup {
  id: string
  tenantId: string
  name: string
  description: string | null
  type: GroupType
  tagQuery: string | null
  deviceIds: string[]
  createdAt: string
  updatedAt: string
}

export interface GroupMembership {
  membershipId: string
  deviceId: string
  deviceName: string
  type: MembershipType
  addedAt: string
  addedBy: string
}

export interface GroupMembershipsResponse {
  deviceGroupId: string
  groupName: string
  memberships: GroupMembership[]
  totalMemberships: number
  staticMemberships: number
  dynamicMemberships: number
}

export interface CreateGroupRequest extends Record<string, unknown> {
  name: string
  description?: string
  type?: GroupType
  tagQuery?: string
  deviceIds?: string[]
}

export interface UpdateGroupRequest extends Record<string, unknown> {
  name?: string
  description?: string
  tagQuery?: string
}

export interface GroupFilters extends PaginationParams {
  search?: string
  type?: GroupType
}
