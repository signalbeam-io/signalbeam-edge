/**
 * Registration Token types
 */

export interface RegistrationToken {
  id: string
  tenantId: string
  token: string
  expiresAt: string | null
  maxUses: number | null
  currentUses: number
  createdAt: string
  createdBy: string
  isActive: boolean
}

export interface CreateRegistrationTokenRequest extends Record<string, unknown> {
  tenantId: string
  expiresAt?: string | null
  maxUses?: number | null
  description?: string
}

export interface RegistrationTokenFilters {
  page?: number
  pageSize?: number
  includeInactive?: boolean
}
