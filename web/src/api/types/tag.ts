/**
 * Tag types
 */

import type { PaginationParams } from './common'
import type { Device } from './device'

export interface TagInfo {
  tag: string
  deviceCount: number
}

export interface GetAllTagsResponse {
  tags: TagInfo[]
  totalTags: number
}

export interface AddDeviceTagRequest {
  deviceId: string
  tag: string
}

export interface RemoveDeviceTagRequest {
  deviceId: string
  tag: string
}

export interface SearchDevicesByTagQueryRequest extends PaginationParams {
  tenantId?: string
  query: string
}

export interface SearchDevicesByTagQueryResponse {
  devices: Device[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
  query: string
}

export interface BulkAddTagsRequest {
  groupId: string
  tag: string
}

export interface BulkRemoveTagsRequest {
  groupId: string
  tag: string
}

export interface BulkOperationResponse {
  devicesUpdated: number
  successfulDeviceIds: string[]
  failedDeviceIds: string[]
  errors: string[]
}
