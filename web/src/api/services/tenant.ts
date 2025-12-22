import { getTenantId } from '@/lib/tenant'

export function appendTenantId(params: URLSearchParams): void {
  if (!params.has('tenantId')) {
    params.append('tenantId', getTenantId())
  }
}

export function withTenantId<T extends Record<string, unknown>>(
  data: T
): T & { tenantId: string } {
  if ('tenantId' in data && data.tenantId) {
    return data as T & { tenantId: string }
  }
  return { ...data, tenantId: getTenantId() }
}
