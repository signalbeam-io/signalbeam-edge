/**
 * Alerts API client
 */

import { apiRequest } from '../client'
import { appendTenantId } from './tenant'
import type {
  AlertDetailResponse,
  AlertFilters,
  AlertListResponse,
  AlertStatistics,
  AcknowledgeAlertRequest,
  AcknowledgeAlertResponse,
  ResolveAlertResponse,
} from '../types'

const BASE_PATH = '/api/alerts'

export const alertsApi = {
  /**
   * Get list of alerts with filtering
   */
  async getAlerts(filters?: AlertFilters): Promise<AlertListResponse> {
    const params = new URLSearchParams()

    appendTenantId(params)

    if (filters?.status) params.append('status', filters.status)
    if (filters?.severity) params.append('severity', filters.severity)
    if (filters?.type) params.append('type', filters.type)
    if (filters?.deviceId) params.append('deviceId', filters.deviceId)
    if (filters?.createdAfter) params.append('createdAfter', filters.createdAfter)
    if (filters?.createdBefore) params.append('createdBefore', filters.createdBefore)
    if (filters?.limit) params.append('limit', filters.limit.toString())
    if (filters?.offset) params.append('offset', filters.offset.toString())

    return apiRequest<AlertListResponse>({
      method: 'GET',
      url: `${BASE_PATH}?${params.toString()}`,
    })
  },

  /**
   * Get alert by ID with notifications
   */
  async getAlertById(id: string): Promise<AlertDetailResponse> {
    return apiRequest<AlertDetailResponse>({
      method: 'GET',
      url: `${BASE_PATH}/${id}`,
    })
  },

  /**
   * Get alert statistics
   */
  async getStatistics(tenantId?: string): Promise<AlertStatistics> {
    const params = new URLSearchParams()

    if (tenantId) {
      params.append('tenantId', tenantId)
    } else {
      appendTenantId(params)
    }

    return apiRequest<AlertStatistics>({
      method: 'GET',
      url: `${BASE_PATH}/statistics?${params.toString()}`,
    })
  },

  /**
   * Acknowledge an alert
   */
  async acknowledgeAlert(
    id: string,
    request: AcknowledgeAlertRequest
  ): Promise<AcknowledgeAlertResponse> {
    return apiRequest<AcknowledgeAlertResponse>({
      method: 'POST',
      url: `${BASE_PATH}/${id}/acknowledge`,
      data: request,
    })
  },

  /**
   * Resolve an alert
   */
  async resolveAlert(id: string): Promise<ResolveAlertResponse> {
    return apiRequest<ResolveAlertResponse>({
      method: 'POST',
      url: `${BASE_PATH}/${id}/resolve`,
    })
  },
}
