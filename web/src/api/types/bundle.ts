/**
 * App Bundle types
 */

import type { PaginationParams } from './common'

export interface AppBundle {
  id: string
  tenantId: string
  name: string
  description: string | null
  currentVersion: string
  versions: BundleVersion[]
  createdAt: string
  updatedAt: string
}

export interface BundleVersion {
  version: string
  containers: ContainerDefinition[]
  createdAt: string
  isActive: boolean
}

export interface ContainerDefinition {
  name: string
  image: string
  tag: string
  environment?: Record<string, string>
  ports?: PortMapping[]
  volumes?: VolumeMapping[]
}

export interface PortMapping {
  container: number
  host: number
  protocol: 'tcp' | 'udp'
}

export interface VolumeMapping {
  hostPath: string
  containerPath: string
  readOnly?: boolean
}

export interface CreateBundleRequest extends Record<string, unknown> {
  name: string
  description?: string
  version?: string
  containers?: ContainerDefinition[]
}

export interface UpdateBundleRequest extends Record<string, unknown> {
  name?: string
  description?: string
}

export interface CreateBundleVersionRequest extends Record<string, unknown> {
  version: string
  containers: ContainerDefinition[]
}

export interface BundleFilters extends PaginationParams {
  search?: string
}
