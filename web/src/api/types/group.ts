/**
 * Device Group types
 */

import type { PaginationParams } from './common'

export interface DeviceGroup {
  id: string
  tenantId: string
  name: string
  description: string | null
  deviceIds: string[]
  createdAt: string
  updatedAt: string
}

export interface CreateGroupRequest {
  name: string
  description?: string
  deviceIds?: string[]
}

export interface UpdateGroupRequest {
  name?: string
  description?: string
  deviceIds?: string[]
}

export interface GroupFilters extends PaginationParams {
  search?: string
}
