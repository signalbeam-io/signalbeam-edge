/**
 * Devices API client
 */

import { apiRequest } from '../client'
import type {
  ContainerDetails,
  ContainerLog,
  Device,
  DeviceActivity,
  DeviceFilters,
  DeviceMetrics,
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

  /**
   * Get device metrics (24h history)
   */
  async getDeviceMetrics(id: string): Promise<DeviceMetrics[]> {
    return apiRequest<DeviceMetrics[]>({
      method: 'GET',
      url: `${BASE_PATH}/${id}/metrics`,
    })
  },

  /**
   * Get device containers
   */
  async getDeviceContainers(id: string): Promise<ContainerDetails[]> {
    return apiRequest<ContainerDetails[]>({
      method: 'GET',
      url: `${BASE_PATH}/${id}/containers`,
    })
  },

  /**
   * Get container logs
   */
  async getContainerLogs(
    deviceId: string,
    containerName: string,
    tail?: number
  ): Promise<ContainerLog[]> {
    const params = new URLSearchParams()
    if (tail) params.append('tail', tail.toString())

    return apiRequest<ContainerLog[]>({
      method: 'GET',
      url: `${BASE_PATH}/${deviceId}/containers/${containerName}/logs?${params.toString()}`,
    })
  },

  /**
   * Get device activity/events
   */
  async getDeviceActivity(
    id: string,
    page: number = 1,
    pageSize: number = 20
  ): Promise<PaginatedResponse<DeviceActivity>> {
    const params = new URLSearchParams()
    params.append('page', page.toString())
    params.append('pageSize', pageSize.toString())

    return apiRequest<PaginatedResponse<DeviceActivity>>({
      method: 'GET',
      url: `${BASE_PATH}/${id}/activity?${params.toString()}`,
    })
  },
}
