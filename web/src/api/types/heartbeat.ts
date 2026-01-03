/**
 * Heartbeat types
 */

import type { PaginationParams } from './common'

export interface DeviceHeartbeat {
  id: string
  deviceId: string
  timestamp: string
  cpuUsage: number
  memoryUsage: number
  diskUsage: number
  uptime: number
  containerStatus: ContainerStatus[]
}

export interface ContainerStatus {
  name: string
  image: string
  status: string
  state: 'running' | 'stopped' | 'error'
}

export interface HeartbeatFilters extends PaginationParams {
  deviceId: string
  startDate?: string
  endDate?: string
}

export interface SendHeartbeatRequest extends Record<string, unknown> {
  deviceId: string
  cpuUsage: number
  memoryUsage: number
  diskUsage: number
  uptime: number
  containerStatus: ContainerStatus[]
}
