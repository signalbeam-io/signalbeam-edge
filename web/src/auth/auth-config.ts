export type AuthMode = 'entra' | 'apiKey'

const configuredAuthMode = import.meta.env.VITE_AUTH_MODE
export const AUTH_MODE: AuthMode = configuredAuthMode === 'entra' ? 'entra' : 'apiKey'

const defaultTenantId = import.meta.env.VITE_ENTRA_TENANT_ID ?? 'common'

export const ENTRA_CLIENT_ID = import.meta.env.VITE_ENTRA_CLIENT_ID ?? ''
export const ENTRA_AUTHORITY =
  import.meta.env.VITE_ENTRA_AUTHORITY ??
  `https://login.microsoftonline.com/${defaultTenantId}`
export const ENTRA_REDIRECT_URI =
  import.meta.env.VITE_ENTRA_REDIRECT_URI ?? `${window.location.origin}/login`
export const ENTRA_POST_LOGOUT_REDIRECT_URI =
  import.meta.env.VITE_ENTRA_POST_LOGOUT_REDIRECT_URI ?? window.location.origin

export const ENTRA_SCOPES = (import.meta.env.VITE_ENTRA_SCOPES ?? 'openid,profile,email')
  .split(',')
  .map((scope) => scope.trim())
  .filter(Boolean)
