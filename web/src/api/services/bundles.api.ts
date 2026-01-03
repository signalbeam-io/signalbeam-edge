/**
 * App Bundles API client
 */

import { apiRequest } from '../client'
import { appendTenantId, withTenantId } from './tenant'
import type {
  AppBundle,
  BundleFilters,
  BundleVersion,
  ContainerDefinition,
  PortMapping,
  VolumeMapping,
  PaginatedResponse,
  CreateBundleRequest,
  UpdateBundleRequest,
  CreateBundleVersionRequest,
} from '../types'

export interface AssignedDevice {
  deviceId: string
  bundleVersion: string
  assignedAt: string
  assignedBy: string | null
}

const BASE_PATH = '/api/bundles'

interface BackendBundleSummary {
  bundleId: string
  name: string
  description: string | null
  latestVersion: string | null
  createdAt: string
  versions: BackendBundleVersionSummary[]
}

interface BackendBundleListResponse {
  bundles: BackendBundleSummary[]
}

interface BackendBundleVersionSummary {
  versionId: string
  version: string
  containers: BackendContainerSpecDetail[]
  containerCount: number
  releaseNotes: string | null
  createdAt: string
}

interface BackendContainerSpecDetail {
  name: string
  image: string
  environmentVariables: string | null
  portMappings: string | null
  volumeMounts: string | null
  additionalParameters: string | null
}

interface BackendBundleDetail {
  bundleId: string
  name: string
  description: string | null
  latestVersion: string | null
  createdAt: string
  versions: BackendBundleVersionSummary[]
}

interface BackendBundleDetailResponse {
  bundle: BackendBundleDetail
}

function mapBundleSummary(bundle: BackendBundleSummary): AppBundle {
  return {
    id: bundle.bundleId,
    tenantId: '',
    name: bundle.name,
    description: bundle.description,
    currentVersion: bundle.latestVersion ?? '',
    versions: bundle.versions.map((version) => mapBundleVersion(version, bundle.latestVersion)),
    createdAt: bundle.createdAt,
    updatedAt: bundle.createdAt,
  }
}

function mapBundleDetail(bundle: BackendBundleDetail): AppBundle {
  return {
    id: bundle.bundleId,
    tenantId: '',
    name: bundle.name,
    description: bundle.description,
    currentVersion: bundle.latestVersion ?? '',
    versions: bundle.versions.map((version) => mapBundleVersion(version, bundle.latestVersion)),
    createdAt: bundle.createdAt,
    updatedAt: bundle.createdAt,
  }
}

function mapBundleVersion(
  version: BackendBundleVersionSummary,
  latestVersion: string | null
): BundleVersion {
  return {
    version: version.version,
    containers: version.containers.map(mapContainerSpec),
    createdAt: version.createdAt,
    isActive: latestVersion === version.version,
  }
}

function mapContainerSpec(container: BackendContainerSpecDetail): ContainerDefinition {
  const { image, tag } = splitImageTag(container.image)
  const environment = parseJsonRecord(container.environmentVariables)
  const ports = parsePortMappings(container.portMappings)
  const volumes = parseVolumeMounts(container.volumeMounts)

  return {
    name: container.name,
    image,
    tag,
    ...(environment && { environment }),
    ...(ports && { ports }),
    ...(volumes && { volumes }),
  }
}

function parseJsonRecord(value: string | null): Record<string, string> | undefined {
  if (!value) return undefined
  try {
    const parsed = JSON.parse(value)
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, string>
    }
  } catch {
    return undefined
  }
  return undefined
}

function parsePortMappings(value: string | null): PortMapping[] | undefined {
  if (!value) return undefined
  try {
    const parsed = JSON.parse(value)
    if (Array.isArray(parsed)) {
      return parsed
        .map((entry) => {
          if (typeof entry === 'string') {
            return parsePortMappingString(entry)
          }
          if (entry && typeof entry === 'object') {
            const { host, container, protocol } = entry as {
              host?: number
              container?: number
              protocol?: 'tcp' | 'udp'
            }
            if (host && container) {
              return { host, container, protocol: protocol ?? 'tcp' }
            }
          }
          return null
        })
        .filter((p): p is PortMapping => p !== null)
    }
  } catch {
    return undefined
  }
  return undefined
}

function parsePortMappingString(value: string) {
  const [mapping, proto] = value.split('/')
  if (!mapping) return null
  const [hostRaw, containerRaw] = mapping.split(':')
  const host = Number(hostRaw)
  const container = Number(containerRaw)
  if (!Number.isFinite(host) || !Number.isFinite(container)) {
    return null
  }
  return {
    host,
    container,
    protocol: proto === 'udp' ? 'udp' : 'tcp',
  }
}

function parseVolumeMounts(value: string | null): VolumeMapping[] | undefined {
  if (!value) return undefined
  try {
    const parsed = JSON.parse(value)
    if (Array.isArray(parsed)) {
      return parsed
        .map((entry) => {
          if (typeof entry === 'string') {
            return parseVolumeMountString(entry)
          }
          if (entry && typeof entry === 'object') {
            const { hostPath, containerPath, readOnly } = entry as {
              hostPath?: string
              containerPath?: string
              readOnly?: boolean
            }
            if (hostPath && containerPath) {
              return {
                hostPath,
                containerPath,
                ...(readOnly !== undefined && { readOnly }),
              }
            }
          }
          return null
        })
        .filter((v): v is VolumeMapping => v !== null)
    }
  } catch {
    return undefined
  }
  return undefined
}

function parseVolumeMountString(value: string) {
  const parts = value.split(':')
  if (parts.length < 2) {
    return null
  }
  return {
    hostPath: parts[0],
    containerPath: parts[1],
    readOnly: parts[2] === 'ro',
  }
}

function splitImageTag(image: string) {
  const lastColon = image.lastIndexOf(':')
  if (lastColon > -1 && !image.slice(lastColon).includes('/')) {
    return {
      image: image.slice(0, lastColon),
      tag: image.slice(lastColon + 1),
    }
  }
  return { image, tag: 'latest' }
}

export const bundlesApi = {
  /**
   * Get paginated list of bundles
   */
  async getBundles(filters?: BundleFilters): Promise<PaginatedResponse<AppBundle>> {
    const params = new URLSearchParams()

    appendTenantId(params)

    if (filters?.page) params.append('page', filters.page.toString())
    if (filters?.pageSize) params.append('pageSize', filters.pageSize.toString())
    if (filters?.search) params.append('search', filters.search)

    const response = await apiRequest<BackendBundleListResponse>({
      method: 'GET',
      url: `${BASE_PATH}?${params.toString()}`,
    })

    const bundles = response.bundles.map(mapBundleSummary)
    return {
      data: bundles,
      total: bundles.length,
      page: filters?.page ?? 1,
      pageSize: filters?.pageSize ?? bundles.length,
      totalPages: 1,
    }
  },

  /**
   * Get bundle by ID
   */
  async getBundle(id: string): Promise<AppBundle> {
    const params = new URLSearchParams()
    appendTenantId(params)

    const response = await apiRequest<BackendBundleDetailResponse>({
      method: 'GET',
      url: `${BASE_PATH}/${id}?${params.toString()}`,
    })

    return mapBundleDetail(response.bundle)
  },

  /**
   * Create a new bundle
   */
  async createBundle(data: CreateBundleRequest): Promise<AppBundle> {
    interface CreateBundleBackendResponse {
      bundleId: string
      name: string
      description: string | null
      createdAt: string
    }

    const response = await apiRequest<CreateBundleBackendResponse>({
      method: 'POST',
      url: BASE_PATH,
      data: withTenantId(data),
    })

    // Map backend response to AppBundle
    return {
      id: response.bundleId,
      tenantId: '',
      name: response.name,
      description: response.description,
      currentVersion: '', // No version yet
      versions: [], // Empty versions array
      createdAt: response.createdAt,
      updatedAt: response.createdAt,
    }
  },

  /**
   * Update bundle
   */
  async updateBundle(id: string, data: UpdateBundleRequest): Promise<AppBundle> {
    return apiRequest<AppBundle>({
      method: 'PATCH',
      url: `${BASE_PATH}/${id}`,
      data: withTenantId(data),
    })
  },

  /**
   * Delete bundle
   */
  async deleteBundle(id: string): Promise<void> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<void>({
      method: 'DELETE',
      url: `${BASE_PATH}/${id}?${params.toString()}`,
    })
  },

  /**
   * Get bundle versions
   */
  async getBundleVersions(bundleId: string): Promise<BundleVersion[]> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<BundleVersion[]>({
      method: 'GET',
      url: `${BASE_PATH}/${bundleId}/versions?${params.toString()}`,
    })
  },

  /**
   * Create a new bundle version
   */
  async createBundleVersion(
    bundleId: string,
    data: CreateBundleVersionRequest
  ): Promise<BundleVersion> {
    interface CreateBundleVersionBackendResponse {
      versionId: string
      bundleId: string
      version: string
      containerCount: number
      createdAt: string
    }

    const response = await apiRequest<CreateBundleVersionBackendResponse>({
      method: 'POST',
      url: `${BASE_PATH}/${bundleId}/versions`,
      data: withTenantId(data),
    })

    // Map backend response to BundleVersion
    // Note: We don't have full container details in the response,
    // so we use the request data
    return {
      version: response.version,
      containers: data.containers.map((c): ContainerDefinition => ({
        name: c.name,
        image: c.image,
        tag: c.tag || 'latest',
        ...(c.environment && { environment: c.environment }),
        ...(c.ports && { ports: c.ports }),
        ...(c.volumes && { volumes: c.volumes }),
      })),
      createdAt: response.createdAt,
      isActive: false, // Will be updated when fetching full bundle details
    }
  },

  /**
   * Set active version
   */
  async setActiveVersion(bundleId: string, version: string): Promise<AppBundle> {
    const params = new URLSearchParams()
    appendTenantId(params)

    return apiRequest<AppBundle>({
      method: 'PUT',
      url: `${BASE_PATH}/${bundleId}/versions/${version}/activate?${params.toString()}`,
    })
  },

  /**
   * Get devices assigned to a bundle
   */
  async getAssignedDevices(bundleId: string): Promise<AssignedDevice[]> {
    interface BackendAssignedDeviceResponse {
      bundleId: string
      assignedDevices: {
        deviceId: string
        bundleVersion: string
        assignedAt: string
        assignedBy: string | null
      }[]
    }

    const params = new URLSearchParams()
    appendTenantId(params)

    const response = await apiRequest<BackendAssignedDeviceResponse>({
      method: 'GET',
      url: `${BASE_PATH}/${bundleId}/assigned-devices?${params.toString()}`,
    })

    return response.assignedDevices
  },
}
