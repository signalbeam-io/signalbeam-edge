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

    if (filters?.page) params.append('pageNumber', filters.page.toString())
    if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString())
    if (filters?.status) params.append('status', filters.status)
    if (filters?.tags?.length) params.append('tag', filters.tags[0])
    if (filters?.groupIds?.length) params.append('deviceGroupId', filters.groupIds[0])

    const response = await apiRequest<BackendDeviceListResponse>({
      method: 'GET',
      url: `${BASE_PATH}?${params.toString()}`,
    })

    return mapDeviceListResponse(response)
  },

  /**
   * Get device by ID
   */
  async getDevice(id: string): Promise<Device> {
    const response = await apiRequest<BackendDeviceResponse>({
      method: 'GET',
      url: `${BASE_PATH}/${id}`,
    })

    return mapDevice(response)
  },

  /**
   * Register a new device
   */
  async registerDevice(data: RegisterDeviceRequest): Promise<Device> {
    const response = await apiRequest<BackendDeviceResponse>({
      method: 'POST',
      url: `${BASE_PATH}`,
      data: {
        tenantId: data.tenantId,
        deviceId: data.deviceId,
        name: data.name ?? data.deviceId,
        metadata: data.metadata ? JSON.stringify(data.metadata) : undefined,
      },
    })

    return mapDevice(response)
  },

  /**
   * Update device
   */
  async updateDevice(id: string, data: UpdateDeviceRequest): Promise<Device> {
    const response = await apiRequest<BackendDeviceResponse>({
      method: 'PUT',
      url: `${BASE_PATH}/${id}`,
      data,
    })

    return mapDevice(response)
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

interface BackendDeviceListResponse {
  devices: BackendDeviceResponse[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
}

interface BackendDeviceResponse {
  id: string
  tenantId: string
  name: string
  status: string
  lastSeenAt: string | null
  registeredAt: string
  metadata: string | null
  tags: string[]
  assignedBundleId: string | null
  bundleDeploymentStatus: string | null
  deviceGroupId: string | null
}

const statusMap: Record<string, Device['status']> = {
  online: 'online',
  offline: 'offline',
  updating: 'updating',
  error: 'error',
  registered: 'offline',
}

function mapDevice(device: BackendDeviceResponse): Device {
  return {
    id: device.id,
    tenantId: device.tenantId,
    name: device.name,
    status: statusMap[device.status.toLowerCase()] ?? 'offline',
    lastHeartbeat: device.lastSeenAt,
    currentBundleId: device.assignedBundleId,
    currentBundleVersion: null,
    tags: device.tags ?? [],
    groupIds: device.deviceGroupId ? [device.deviceGroupId] : [],
    metadata: parseMetadata(device.metadata),
    createdAt: device.registeredAt,
    updatedAt: device.lastSeenAt ?? device.registeredAt,
  }
}

function mapDeviceListResponse(response: BackendDeviceListResponse): PaginatedResponse<Device> {
  return {
    data: response.devices.map(mapDevice),
    total: response.totalCount,
    page: response.pageNumber,
    pageSize: response.pageSize,
    totalPages: response.totalPages,
  }
}

function parseMetadata(metadata: string | null): Record<string, unknown> {
  if (!metadata) {
    return {}
  }
  try {
    const parsed = JSON.parse(metadata)
    if (parsed && typeof parsed === 'object') {
      return parsed as Record<string, unknown>
    }
  } catch {
    return {}
  }
  return {}
}
