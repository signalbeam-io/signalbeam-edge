/**
 * App Bundles API client
 */

import { apiRequest } from '../client'
import type {
  AppBundle,
  BundleFilters,
  BundleVersion,
  PaginatedResponse,
  CreateBundleRequest,
  UpdateBundleRequest,
  CreateBundleVersionRequest,
} from '../types'

const BASE_PATH = '/api/bundles'

export const bundlesApi = {
  /**
   * Get paginated list of bundles
   */
  async getBundles(filters?: BundleFilters): Promise<PaginatedResponse<AppBundle>> {
    const params = new URLSearchParams()

    if (filters?.page) params.append('page', filters.page.toString())
    if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString())
    if (filters?.search) params.append('search', filters.search)

    return apiRequest<PaginatedResponse<AppBundle>>({
      method: 'GET',
      url: `${BASE_PATH}?${params.toString()}`,
    })
  },

  /**
   * Get bundle by ID
   */
  async getBundle(id: string): Promise<AppBundle> {
    return apiRequest<AppBundle>({
      method: 'GET',
      url: `${BASE_PATH}/${id}`,
    })
  },

  /**
   * Create a new bundle
   */
  async createBundle(data: CreateBundleRequest): Promise<AppBundle> {
    return apiRequest<AppBundle>({
      method: 'POST',
      url: BASE_PATH,
      data,
    })
  },

  /**
   * Update bundle
   */
  async updateBundle(id: string, data: UpdateBundleRequest): Promise<AppBundle> {
    return apiRequest<AppBundle>({
      method: 'PATCH',
      url: `${BASE_PATH}/${id}`,
      data,
    })
  },

  /**
   * Delete bundle
   */
  async deleteBundle(id: string): Promise<void> {
    return apiRequest<void>({
      method: 'DELETE',
      url: `${BASE_PATH}/${id}`,
    })
  },

  /**
   * Get bundle versions
   */
  async getBundleVersions(bundleId: string): Promise<BundleVersion[]> {
    return apiRequest<BundleVersion[]>({
      method: 'GET',
      url: `${BASE_PATH}/${bundleId}/versions`,
    })
  },

  /**
   * Create a new bundle version
   */
  async createBundleVersion(
    bundleId: string,
    data: CreateBundleVersionRequest
  ): Promise<BundleVersion> {
    return apiRequest<BundleVersion>({
      method: 'POST',
      url: `${BASE_PATH}/${bundleId}/versions`,
      data,
    })
  },

  /**
   * Set active version
   */
  async setActiveVersion(bundleId: string, version: string): Promise<AppBundle> {
    return apiRequest<AppBundle>({
      method: 'PUT',
      url: `${BASE_PATH}/${bundleId}/versions/${version}/activate`,
    })
  },
}
