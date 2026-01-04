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
  PhasedRollout,
  PhasedRolloutFilters,
  CreatePhasedRolloutRequest,
  ActiveRollout,
} from '../types'

const BASE_PATH = '/api/rollouts'
const PHASED_BASE_PATH = '/api/phased-rollouts'

export const rolloutsApi = {
  /**
   * Get paginated list of rollouts (legacy simple rollouts)
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
   * Get rollout by ID (legacy simple rollout)
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
   * Create a new rollout (legacy simple rollout)
   */
  async createRollout(data: CreateRolloutRequest): Promise<Rollout> {
    return apiRequest<Rollout>({
      method: 'POST',
      url: BASE_PATH,
      data: withTenantId(data),
    })
  },

  /**
   * Cancel rollout (legacy)
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
   * Get device-level rollout status (legacy)
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
   * Retry failed devices in a rollout (legacy)
   */
  async retryFailedDevices(rolloutId: string): Promise<Rollout> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<Rollout>({
      method: 'POST',
      url: `${BASE_PATH}/${rolloutId}/retry?${params.toString()}`,
    })
  },

  // Phased Rollout APIs (NEW)

  /**
   * Get paginated list of phased rollouts
   */
  async getPhasedRollouts(filters?: PhasedRolloutFilters): Promise<PaginatedResponse<PhasedRollout>> {
    const params = new URLSearchParams()

    appendTenantId(params)
    if (filters?.page) params.append('page', filters.page.toString())
    if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString())
    if (filters?.bundleId) params.append('bundleId', filters.bundleId)
    if (filters?.status) params.append('status', filters.status)

    return apiRequest<PaginatedResponse<PhasedRollout>>({
      method: 'GET',
      url: `${PHASED_BASE_PATH}?${params.toString()}`,
    })
  },

  /**
   * Get phased rollout by ID
   */
  async getPhasedRollout(id: string): Promise<PhasedRollout> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<PhasedRollout>({
      method: 'GET',
      url: `${PHASED_BASE_PATH}/${id}?${params.toString()}`,
    })
  },

  /**
   * Create a new phased rollout
   */
  async createPhasedRollout(data: CreatePhasedRolloutRequest): Promise<PhasedRollout> {
    return apiRequest<PhasedRollout>({
      method: 'POST',
      url: PHASED_BASE_PATH,
      data: withTenantId(data),
    })
  },

  /**
   * Start a phased rollout
   */
  async startPhasedRollout(id: string): Promise<PhasedRollout> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<PhasedRollout>({
      method: 'POST',
      url: `${PHASED_BASE_PATH}/${id}/start?${params.toString()}`,
    })
  },

  /**
   * Pause a phased rollout
   */
  async pausePhasedRollout(id: string): Promise<PhasedRollout> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<PhasedRollout>({
      method: 'POST',
      url: `${PHASED_BASE_PATH}/${id}/pause?${params.toString()}`,
    })
  },

  /**
   * Resume a paused phased rollout
   */
  async resumePhasedRollout(id: string): Promise<PhasedRollout> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<PhasedRollout>({
      method: 'POST',
      url: `${PHASED_BASE_PATH}/${id}/resume?${params.toString()}`,
    })
  },

  /**
   * Rollback a phased rollout to previous version
   */
  async rollbackPhasedRollout(id: string): Promise<PhasedRollout> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<PhasedRollout>({
      method: 'POST',
      url: `${PHASED_BASE_PATH}/${id}/rollback?${params.toString()}`,
    })
  },

  /**
   * Get active rollouts for a tenant
   */
  async getActiveRollouts(): Promise<ActiveRollout[]> {
    const params = new URLSearchParams()
    appendTenantId(params)

    const response = await apiRequest<{ activeRollouts: ActiveRollout[]; totalCount: number }>({
      method: 'GET',
      url: `${PHASED_BASE_PATH}/active?${params.toString()}`,
    })

    // Extract the activeRollouts array from the response object
    return response.activeRollouts
  },
}
