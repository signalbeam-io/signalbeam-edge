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

/**
 * Device metrics for charts (24h aggregated data)
 */
export interface DeviceMetrics {
  timestamp: string
  cpuUsage: number
  memoryUsage: number
  diskUsage: number
}

/**
 * Device activity/event entry
 */
export interface DeviceActivity {
  id: string
  deviceId: string
  eventType: DeviceEventType
  message: string
  metadata?: Record<string, unknown>
  timestamp: string
}

export enum DeviceEventType {
  Registered = 'registered',
  Updated = 'updated',
  StatusChanged = 'status_changed',
  BundleAssigned = 'bundle_assigned',
  BundleUpdated = 'bundle_updated',
  ContainerStarted = 'container_started',
  ContainerStopped = 'container_stopped',
  ContainerError = 'container_error',
  HeartbeatMissed = 'heartbeat_missed',
  HeartbeatResumed = 'heartbeat_resumed',
}

/**
 * Container log entry
 */
export interface ContainerLog {
  timestamp: string
  container: string
  level: 'info' | 'warn' | 'error' | 'debug'
  message: string
}

/**
 * Container details with extended information
 */
export interface ContainerDetails {
  name: string
  image: string
  status: string
  state: 'running' | 'stopped' | 'error'
  uptime?: number
  restartCount?: number
  ports?: string[]
  createdAt?: string
}
