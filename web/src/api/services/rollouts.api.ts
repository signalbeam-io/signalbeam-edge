/**
 * Rollouts API client
 */

import { apiRequest } from '../client'
import { appendTenantId, withTenantId } from './tenant'
import type {
  Rollout,
  RolloutFilters,
  PaginatedResponse,
  CreateRolloutRequest,
  DeviceRolloutStatus,
} from '../types'

const BASE_PATH = '/api/rollouts'

export const rolloutsApi = {
  /**
   * Get paginated list of rollouts
   */
  async getRollouts(filters?: RolloutFilters): Promise<PaginatedResponse<Rollout>> {
    const params = new URLSearchParams()

    appendTenantId(params)
    if (filters?.page) params.append('page', filters.page.toString())
    if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString())
    if (filters?.bundleId) params.append('bundleId', filters.bundleId)
    if (filters?.status) params.append('status', filters.status)

    return apiRequest<PaginatedResponse<Rollout>>({
      method: 'GET',
      url: `${BASE_PATH}?${params.toString()}`,
    })
  },

  /**
   * Get rollout by ID
   */
  async getRollout(id: string): Promise<Rollout> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<Rollout>({
      method: 'GET',
      url: `${BASE_PATH}/${id}?${params.toString()}`,
    })
  },

  /**
   * Create a new rollout
   */
  async createRollout(data: CreateRolloutRequest): Promise<Rollout> {
    return apiRequest<Rollout>({
      method: 'POST',
      url: BASE_PATH,
      data: withTenantId(data),
    })
  },

  /**
   * Cancel rollout
   */
  async cancelRollout(id: string): Promise<Rollout> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<Rollout>({
      method: 'POST',
      url: `${BASE_PATH}/${id}/cancel?${params.toString()}`,
    })
  },

  /**
   * Get device-level rollout status
   */
  async getDeviceRolloutStatus(rolloutId: string): Promise<DeviceRolloutStatus[]> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<DeviceRolloutStatus[]>({
      method: 'GET',
      url: `${BASE_PATH}/${rolloutId}/devices?${params.toString()}`,
    })
  },

  /**
   * Retry failed devices in a rollout
   */
  async retryFailedDevices(rolloutId: string): Promise<Rollout> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<Rollout>({
      method: 'POST',
      url: `${BASE_PATH}/${rolloutId}/retry?${params.toString()}`,
    })
  },
}
