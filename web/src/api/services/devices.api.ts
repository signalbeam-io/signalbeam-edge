/**
 * Devices API client
 */

import { apiRequest } from '../client'
import type {
  Device,
  DeviceFilters,
  PaginatedResponse,
  RegisterDeviceRequest,
  UpdateDeviceRequest,
} from '../types'

const BASE_PATH = '/api/devices'

export const devicesApi = {
  /**
   * Get paginated list of devices
   */
  async getDevices(filters?: DeviceFilters): Promise<PaginatedResponse<Device>> {
    const params = new URLSearchParams()

    if (filters?.page) params.append('page', filters.page.toString())
    if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString())
    if (filters?.status) params.append('status', filters.status)
    if (filters?.search) params.append('search', filters.search)
    if (filters?.tags?.length) {
      filters.tags.forEach(tag => params.append('tags', tag))
    }
    if (filters?.groupIds?.length) {
      filters.groupIds.forEach(id => params.append('groupIds', id))
    }

    return apiRequest<PaginatedResponse<Device>>({
      method: 'GET',
      url: `${BASE_PATH}?${params.toString()}`,
    })
  },

  /**
   * Get device by ID
   */
  async getDevice(id: string): Promise<Device> {
    return apiRequest<Device>({
      method: 'GET',
      url: `${BASE_PATH}/${id}`,
    })
  },

  /**
   * Register a new device
   */
  async registerDevice(data: RegisterDeviceRequest): Promise<Device> {
    return apiRequest<Device>({
      method: 'POST',
      url: `${BASE_PATH}/register`,
      data,
    })
  },

  /**
   * Update device
   */
  async updateDevice(id: string, data: UpdateDeviceRequest): Promise<Device> {
    return apiRequest<Device>({
      method: 'PATCH',
      url: `${BASE_PATH}/${id}`,
      data,
    })
  },

  /**
   * Delete device
   */
  async deleteDevice(id: string): Promise<void> {
    return apiRequest<void>({
      method: 'DELETE',
      url: `${BASE_PATH}/${id}`,
    })
  },
}
