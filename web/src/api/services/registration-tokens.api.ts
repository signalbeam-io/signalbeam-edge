/**
 * Registration Tokens API client
 */

import { apiRequest } from '../client'
import { appendTenantId, withTenantId } from './tenant'
import type {
  RegistrationToken,
  CreateRegistrationTokenRequest,
  RegistrationTokenFilters,
} from '../types/registration-token'
import type { PaginatedResponse } from '../types'

const BASE_PATH = '/api/registration-tokens'

export const registrationTokensApi = {
  /**
   * Get paginated list of registration tokens
   */
  async getTokens(
    filters?: RegistrationTokenFilters
  ): Promise<PaginatedResponse<RegistrationToken>> {
    const params = new URLSearchParams()
    appendTenantId(params)

    if (filters?.page) params.append('pageNumber', filters.page.toString())
    if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString())
    if (filters?.includeInactive) params.append('includeInactive', 'true')

    return apiRequest<PaginatedResponse<RegistrationToken>>({
      method: 'GET',
      url: `${BASE_PATH}?${params.toString()}`,
    })
  },

  /**
   * Create a new registration token
   */
  async createToken(data: CreateRegistrationTokenRequest): Promise<RegistrationToken> {
    return apiRequest<RegistrationToken>({
      method: 'POST',
      url: `${BASE_PATH}`,
      data: withTenantId(data),
    })
  },

  /**
   * Revoke a registration token
   */
  async revokeToken(id: string): Promise<void> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<void>({
      method: 'DELETE',
      url: `${BASE_PATH}/${id}?${params.toString()}`,
    })
  },
}
