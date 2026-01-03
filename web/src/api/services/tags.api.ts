/**
 * Tags API client
 */

import { apiRequest } from '../client'
import { appendTenantId, withTenantId } from './tenant'
import type {
  GetAllTagsResponse,
  AddDeviceTagRequest,
  RemoveDeviceTagRequest,
  SearchDevicesByTagQueryRequest,
  SearchDevicesByTagQueryResponse,
  BulkAddTagsRequest,
  BulkRemoveTagsRequest,
  BulkOperationResponse,
} from '../types'

const BASE_PATH = '/api'

export const tagsApi = {
  /**
   * Get all tags across devices with usage counts
   */
  async getAllTags(): Promise<GetAllTagsResponse> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<GetAllTagsResponse>({
      method: 'GET',
      url: `${BASE_PATH}/tags?${params.toString()}`,
    })
  },

  /**
   * Add tag to a device
   */
  async addDeviceTag(request: AddDeviceTagRequest): Promise<void> {
    return apiRequest<void>({
      method: 'POST',
      url: `${BASE_PATH}/devices/${request.deviceId}/tags`,
      data: withTenantId({
        deviceId: request.deviceId,
        tag: request.tag,
      }),
    })
  },

  /**
   * Remove tag from a device
   */
  async removeDeviceTag(request: RemoveDeviceTagRequest): Promise<void> {
    return apiRequest<void>({
      method: 'DELETE',
      url: `${BASE_PATH}/tags/${request.deviceId}/tags/${encodeURIComponent(request.tag)}`,
    })
  },

  /**
   * Search devices by tag query expression
   */
  async searchDevicesByTagQuery(
    request: SearchDevicesByTagQueryRequest
  ): Promise<SearchDevicesByTagQueryResponse> {
    const params = new URLSearchParams()
    appendTenantId(params)
    params.append('query', request.query)
    if (request.page) params.append('pageNumber', request.page.toString())
    if (request.pageSize) params.append('pageSize', request.pageSize.toString())

    return apiRequest<SearchDevicesByTagQueryResponse>({
      method: 'GET',
      url: `${BASE_PATH}/devices/search?${params.toString()}`,
    })
  },

  /**
   * Bulk add tag to all devices in a group
   */
  async bulkAddTags(request: BulkAddTagsRequest): Promise<BulkOperationResponse> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<BulkOperationResponse>({
      method: 'POST',
      url: `${BASE_PATH}/groups/${request.groupId}/bulk/add-tag?${params.toString()}`,
      data: {
        tag: request.tag,
      },
    })
  },

  /**
   * Bulk remove tag from all devices in a group
   */
  async bulkRemoveTags(request: BulkRemoveTagsRequest): Promise<BulkOperationResponse> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<BulkOperationResponse>({
      method: 'POST',
      url: `${BASE_PATH}/groups/${request.groupId}/bulk/remove-tag?${params.toString()}`,
      data: {
        tag: request.tag,
      },
    })
  },
}
