/**
 * Alert types
 */

import type { PaginationParams } from './common'

export interface Alert {
  id: string
  tenantId: string
  severity: AlertSeverity
  type: AlertType
  status: AlertStatus
  title: string
  description: string
  deviceId: string | null
  rolloutId: string | null
  createdAt: string
  acknowledgedBy: string | null
  acknowledgedAt: string | null
  resolvedAt: string | null
  age: string // TimeSpan format from backend
  timeToAcknowledge: string | null
  timeToResolve: string | null
}

export enum AlertSeverity {
  Info = 'Info',
  Warning = 'Warning',
  Critical = 'Critical',
}

export enum AlertType {
  DeviceOffline = 'DeviceOffline',
  LowBattery = 'LowBattery',
  HighCpuUsage = 'HighCpuUsage',
  HighMemoryUsage = 'HighMemoryUsage',
  HighDiskUsage = 'HighDiskUsage',
  RolloutFailed = 'RolloutFailed',
}

export enum AlertStatus {
  Active = 'Active',
  Acknowledged = 'Acknowledged',
  Resolved = 'Resolved',
}

export interface AlertFilters extends PaginationParams {
  status?: AlertStatus
  severity?: AlertSeverity
  type?: AlertType
  deviceId?: string
  tenantId?: string
  createdAfter?: string
  createdBefore?: string
  limit?: number
  offset?: number
}

export interface AlertListResponse {
  alerts: Alert[]
  totalCount: number
  offset: number
  limit: number
}

export interface AlertNotification {
  id: string
  alertId: string
  channel: string
  recipient: string
  sentAt: string
  success: boolean
  error: string | null
}

export interface AlertDetailResponse {
  alert: Alert | null
  notifications: AlertNotification[]
}

export interface AlertStatistics {
  totalActive: number
  totalAcknowledged: number
  totalResolved: number
  bySeverity: {
    info: number
    warning: number
    critical: number
  }
  byType: {
    counts: Record<string, number>
  }
  staleAlerts: StaleAlertInfo[]
}

export interface StaleAlertInfo {
  alertId: string
  type: AlertType
  severity: AlertSeverity
  deviceId: string | null
  createdAt: string
  age: string
}

export interface AcknowledgeAlertRequest {
  acknowledgedBy: string
}

export interface AcknowledgeAlertResponse {
  success: boolean
  errorMessage: string | null
  alertId: string | null
}

export interface ResolveAlertResponse {
  success: boolean
  errorMessage: string | null
  alertId: string | null
}

/**
 * Helper functions
 */

export const alertSeverityColors = {
  [AlertSeverity.Info]: 'blue',
  [AlertSeverity.Warning]: 'yellow',
  [AlertSeverity.Critical]: 'red',
} as const

export const alertStatusColors = {
  [AlertStatus.Active]: 'red',
  [AlertStatus.Acknowledged]: 'yellow',
  [AlertStatus.Resolved]: 'green',
} as const

export const alertTypeLabels = {
  [AlertType.DeviceOffline]: 'Device Offline',
  [AlertType.LowBattery]: 'Low Battery',
  [AlertType.HighCpuUsage]: 'High CPU Usage',
  [AlertType.HighMemoryUsage]: 'High Memory Usage',
  [AlertType.HighDiskUsage]: 'High Disk Usage',
  [AlertType.RolloutFailed]: 'Rollout Failed',
} as const
