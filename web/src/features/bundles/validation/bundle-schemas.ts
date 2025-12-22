/**
 * Zod validation schemas for bundle forms
 */

import { z } from 'zod'

/**
 * Semantic version regex pattern
 */
const semanticVersionPattern = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$/

/**
 * Port mapping schema
 */
export const portMappingSchema = z.object({
  container: z.number().int().min(1).max(65535),
  host: z.number().int().min(1).max(65535),
  protocol: z.enum(['tcp', 'udp']),
})

/**
 * Volume mapping schema
 */
export const volumeMappingSchema = z.object({
  hostPath: z.string().min(1, 'Host path is required'),
  containerPath: z.string().min(1, 'Container path is required'),
  readOnly: z.boolean().optional(),
})

/**
 * Container definition schema
 */
export const containerDefinitionSchema = z.object({
  name: z
    .string()
    .min(1, 'Container name is required')
    .regex(/^[a-z0-9-]+$/, 'Container name must be lowercase alphanumeric with hyphens'),
  image: z.string().min(1, 'Image is required'),
  tag: z.string().min(1, 'Tag is required'),
  environment: z.record(z.string()).optional(),
  ports: z.array(portMappingSchema).optional(),
  volumes: z.array(volumeMappingSchema).optional(),
})

/**
 * Create bundle schema (with optional initial version)
 */
export const createBundleSchema = z.object({
  name: z
    .string()
    .min(1, 'Bundle name is required')
    .max(100, 'Bundle name must be less than 100 characters'),
  description: z.string().max(500, 'Description must be less than 500 characters').optional(),
  version: z
    .string()
    .regex(semanticVersionPattern, 'Version must be in semantic versioning format (e.g., 1.0.0)')
    .optional(),
  containers: z
    .array(containerDefinitionSchema)
    .min(1, 'At least one container is required')
    .max(10, 'Maximum 10 containers per bundle')
    .optional(),
}).refine(
  (data) => {
    // Version and containers must both be provided or both be omitted
    const hasVersion = !!data.version
    const hasContainers = !!data.containers && data.containers.length > 0
    return hasVersion === hasContainers
  },
  {
    message: 'Version and containers must both be provided or both be omitted',
    path: ['version'],
  }
)

/**
 * Update bundle schema
 */
export const updateBundleSchema = z.object({
  name: z
    .string()
    .min(1, 'Bundle name is required')
    .max(100, 'Bundle name must be less than 100 characters')
    .optional(),
  description: z.string().max(500, 'Description must be less than 500 characters').optional(),
})

/**
 * Create bundle version schema
 */
export const createBundleVersionSchema = z.object({
  version: z
    .string()
    .regex(semanticVersionPattern, 'Version must be in semantic versioning format (e.g., 1.0.0)'),
  containers: z
    .array(containerDefinitionSchema)
    .min(1, 'At least one container is required')
    .max(10, 'Maximum 10 containers per bundle'),
})

/**
 * Assign bundle schema
 */
export const assignBundleSchema = z.object({
  groupIds: z.array(z.string()).optional(),
  deviceIds: z.array(z.string()).optional(),
}).refine(
  (data) => (data.groupIds && data.groupIds.length > 0) || (data.deviceIds && data.deviceIds.length > 0),
  {
    message: 'At least one group or device must be selected',
    path: ['deviceIds'],
  }
)

/**
 * Inferred types from schemas
 */
export type CreateBundleFormData = z.infer<typeof createBundleSchema>
export type UpdateBundleFormData = z.infer<typeof updateBundleSchema>
export type CreateBundleVersionFormData = z.infer<typeof createBundleVersionSchema>
export type AssignBundleFormData = z.infer<typeof assignBundleSchema>
export type ContainerDefinitionFormData = z.infer<typeof containerDefinitionSchema>
export type PortMappingFormData = z.infer<typeof portMappingSchema>
export type VolumeMappingFormData = z.infer<typeof volumeMappingSchema>
