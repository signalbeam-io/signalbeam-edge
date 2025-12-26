/**
 * Rollout types
 */

import type { PaginationParams } from './common'

// Legacy simple rollout (kept for backward compatibility)
export interface Rollout {
  id: string
  tenantId: string
  bundleId: string
  version: string
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
  version: string
  targetType: 'device' | 'group'
  targetIds: string[]
}

export interface RolloutFilters extends PaginationParams {
  bundleId?: string
  status?: RolloutStatus
}

// Phased Rollout Types (NEW)

export interface PhasedRollout {
  id: string
  tenantId: string
  bundleId: string
  targetVersion: string
  previousVersion: string | null
  name: string
  description: string | null
  ownerId: string | null
  createdBy: string | null
  failureThreshold: number
  status: RolloutLifecycleStatus
  currentPhaseNumber: number | null
  phases: RolloutPhase[]
  createdAt: string
  startedAt: string | null
  completedAt: string | null
  updatedAt: string
}

export enum RolloutLifecycleStatus {
  Pending = 'Pending',
  InProgress = 'InProgress',
  Paused = 'Paused',
  Completed = 'Completed',
  RolledBack = 'RolledBack',
  Failed = 'Failed',
}

export interface RolloutPhase {
  id: string
  rolloutId: string
  phaseNumber: number
  name: string
  targetDeviceCount: number | null
  targetPercentage: number
  minHealthyDuration: string | null // ISO 8601 duration
  status: PhaseStatus
  successCount: number
  failureCount: number
  startedAt: string | null
  completedAt: string | null
  deviceAssignments: RolloutDeviceAssignment[]
}

export enum PhaseStatus {
  Pending = 'Pending',
  InProgress = 'InProgress',
  Completed = 'Completed',
  Failed = 'Failed',
}

export interface RolloutDeviceAssignment {
  id: string
  rolloutId: string
  phaseId: string
  deviceId: string
  status: AssignmentStatus
  assignedAt: string | null
  succeededAt: string | null
  failedAt: string | null
  errorMessage: string | null
  retryCount: number
}

export enum AssignmentStatus {
  Pending = 'Pending',
  Assigned = 'Assigned',
  Reconciling = 'Reconciling',
  Succeeded = 'Succeeded',
  Failed = 'Failed',
}

// Phased Rollout Request DTOs

export interface PhaseConfig {
  phaseNumber: number
  name: string
  targetDeviceCount: number | null
  targetPercentage: number
  minHealthyDurationMinutes: number | null
}

export interface CreatePhasedRolloutRequest {
  bundleId: string
  targetVersion: string
  previousVersion?: string
  name: string
  description?: string
  failureThreshold: number
  phases: PhaseConfig[]
}

export interface PhasedRolloutFilters extends PaginationParams {
  bundleId?: string
  status?: RolloutLifecycleStatus
}
