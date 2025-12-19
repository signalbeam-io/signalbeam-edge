/**
 * Device types
 */

import type { PaginationParams } from './common'

export interface Device {
  id: string
  tenantId: string
  name: string
  status: DeviceStatus
  lastHeartbeat: string | null
  currentBundleId: string | null
  currentBundleVersion: string | null
  tags: string[]
  groupIds: string[]
  metadata: Record<string, unknown>
  createdAt: string
  updatedAt: string
}

export enum DeviceStatus {
  Online = 'online',
  Offline = 'offline',
  Updating = 'updating',
  Error = 'error',
}

export interface DeviceFilters extends PaginationParams {
  status?: DeviceStatus | undefined
  tags?: string[] | undefined
  groupIds?: string[] | undefined
  search?: string | undefined
}

export interface RegisterDeviceRequest {
  tenantId: string
  deviceId: string
  registrationToken: string
  name?: string
  metadata?: Record<string, unknown>
}

export interface UpdateDeviceRequest {
  name?: string
  tags?: string[]
  groupIds?: string[]
  metadata?: Record<string, unknown>
}
