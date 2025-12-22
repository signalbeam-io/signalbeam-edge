/**
 * Heartbeat API client
 */

import { apiRequest } from '../client'
import { appendTenantId, withTenantId } from './tenant'
import type {
  DeviceHeartbeat,
  HeartbeatFilters,
  PaginatedResponse,
  SendHeartbeatRequest,
} from '../types'

const BASE_PATH = '/api/heartbeat'

export const heartbeatApi = {
  /**
   * Get device heartbeat history
   */
  async getHeartbeats(filters: HeartbeatFilters): Promise<PaginatedResponse<DeviceHeartbeat>> {
    const params = new URLSearchParams()

    appendTenantId(params)
    params.append('deviceId', filters.deviceId)
    if (filters.page) params.append('page', filters.page.toString())
    if (filters.pageSize) params.append('pageSize', filters.pageSize.toString())
    if (filters.startDate) params.append('startDate', filters.startDate)
    if (filters.endDate) params.append('endDate', filters.endDate)

    return apiRequest<PaginatedResponse<DeviceHeartbeat>>({
      method: 'GET',
      url: `${BASE_PATH}?${params.toString()}`,
    })
  },

  /**
   * Get latest heartbeat for a device
   */
  async getLatestHeartbeat(deviceId: string): Promise<DeviceHeartbeat | null> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<DeviceHeartbeat | null>({
      method: 'GET',
      url: `${BASE_PATH}/${deviceId}/latest?${params.toString()}`,
    })
  },

  /**
   * Send heartbeat (used by edge agent)
   */
  async sendHeartbeat(data: SendHeartbeatRequest): Promise<void> {
    return apiRequest<void>({
      method: 'POST',
      url: BASE_PATH,
      data: withTenantId(data),
    })
  },
}
