/**
 * Device Groups API client
 */

import { apiRequest } from '../client'
import { appendTenantId, withTenantId } from './tenant'
import type {
  DeviceGroup,
  GroupFilters,
  GroupMembershipsResponse,
  PaginatedResponse,
  CreateGroupRequest,
  UpdateGroupRequest,
} from '../types'

const BASE_PATH = '/api/groups'

export const groupsApi = {
  /**
   * Get paginated list of groups
   */
  async getGroups(filters?: GroupFilters): Promise<PaginatedResponse<DeviceGroup>> {
    const params = new URLSearchParams()

    appendTenantId(params)

    if (filters?.page) params.append('page', filters.page.toString())
    if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString())
    if (filters?.search) params.append('search', filters.search)

    return apiRequest<PaginatedResponse<DeviceGroup>>({
      method: 'GET',
      url: `${BASE_PATH}?${params.toString()}`,
    })
  },

  /**
   * Get group by ID
   */
  async getGroup(id: string): Promise<DeviceGroup> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<DeviceGroup>({
      method: 'GET',
      url: `${BASE_PATH}/${id}?${params.toString()}`,
    })
  },

  /**
   * Create a new group
   */
  async createGroup(data: CreateGroupRequest): Promise<DeviceGroup> {
    return apiRequest<DeviceGroup>({
      method: 'POST',
      url: BASE_PATH,
      data: withTenantId(data),
    })
  },

  /**
   * Update group
   */
  async updateGroup(id: string, data: UpdateGroupRequest): Promise<DeviceGroup> {
    return apiRequest<DeviceGroup>({
      method: 'PATCH',
      url: `${BASE_PATH}/${id}`,
      data: withTenantId(data),
    })
  },

  /**
   * Delete group
   */
  async deleteGroup(id: string): Promise<void> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<void>({
      method: 'DELETE',
      url: `${BASE_PATH}/${id}?${params.toString()}`,
    })
  },

  /**
   * Add devices to group
   */
  async addDevicesToGroup(id: string, deviceIds: string[]): Promise<DeviceGroup> {
    return apiRequest<DeviceGroup>({
      method: 'POST',
      url: `${BASE_PATH}/${id}/devices`,
      data: withTenantId({ deviceIds }),
    })
  },

  /**
   * Remove devices from group
   */
  async removeDevicesFromGroup(id: string, deviceIds: string[]): Promise<DeviceGroup> {
    return apiRequest<DeviceGroup>({
      method: 'DELETE',
      url: `${BASE_PATH}/${id}/devices`,
      data: withTenantId({ deviceIds }),
    })
  },

  /**
   * Get group memberships (both static and dynamic)
   */
  async getGroupMemberships(id: string): Promise<GroupMembershipsResponse> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<GroupMembershipsResponse>({
      method: 'GET',
      url: `${BASE_PATH}/${id}/memberships?${params.toString()}`,
    })
  },

  /**
   * Add device to static group
   */
  async addDeviceToGroup(groupId: string, deviceId: string): Promise<void> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<void>({
      method: 'POST',
      url: `${BASE_PATH}/${groupId}/devices/${deviceId}?${params.toString()}`,
    })
  },

  /**
   * Remove device from static group
   */
  async removeDeviceFromGroup(groupId: string, deviceId: string): Promise<void> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<void>({
      method: 'DELETE',
      url: `${BASE_PATH}/${groupId}/devices/${deviceId}?${params.toString()}`,
    })
  },
}
