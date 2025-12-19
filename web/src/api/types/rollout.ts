/**
 * Rollout types
 */

import type { PaginationParams } from './common'

export interface Rollout {
  id: string
  tenantId: string
  bundleId: string
  bundleVersion: string
  targetType: 'device' | 'group'
  targetIds: string[]
  status: RolloutStatus
  progress: RolloutProgress
  createdAt: string
  updatedAt: string
  completedAt: string | null
}

export enum RolloutStatus {
  Pending = 'pending',
  InProgress = 'in_progress',
  Completed = 'completed',
  Failed = 'failed',
  Cancelled = 'cancelled',
}

export interface RolloutProgress {
  total: number
  succeeded: number
  failed: number
  pending: number
  inProgress: number
}

export interface DeviceRolloutStatus {
  deviceId: string
  deviceName: string
  status: DeviceRolloutState
  startedAt: string | null
  completedAt: string | null
  error: string | null
}

export enum DeviceRolloutState {
  Pending = 'pending',
  Updating = 'updating',
  Succeeded = 'succeeded',
  Failed = 'failed',
}

export interface CreateRolloutRequest {
  bundleId: string
  bundleVersion: string
  targetType: 'device' | 'group'
  targetIds: string[]
}

export interface RolloutFilters extends PaginationParams {
  bundleId?: string
  status?: RolloutStatus
}
