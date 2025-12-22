/**
 * Tenant context utilities
 *
 * For MVP, we use a single tenant ID from environment variables.
 * In production, this would come from authentication context (JWT claims).
 */

/**
 * Get the current tenant ID
 *
 * In development: Uses VITE_TENANT_ID environment variable
 * In production: Would extract from authentication token/claims
 */
export function getTenantId(): string {
  const tenantId = import.meta.env.VITE_TENANT_ID

  if (!tenantId) {
    throw new Error(
      'VITE_TENANT_ID is not configured. Please add it to your .env file.'
    )
  }

  return tenantId
}

/**
 * Check if tenant ID is configured
 */
export function hasTenantId(): boolean {
  return !!import.meta.env.VITE_TENANT_ID
}
