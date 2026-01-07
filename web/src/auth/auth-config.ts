export type AuthMode = 'zitadel' | 'entra' | 'apiKey'

const configuredAuthMode = import.meta.env.VITE_AUTH_MODE
export const AUTH_MODE: AuthMode =
  configuredAuthMode === 'entra'
    ? 'entra'
    : configuredAuthMode === 'apiKey'
      ? 'apiKey'
      : 'zitadel' // Default to Zitadel for production-ready multi-tenant auth

// Zitadel Configuration
export const ZITADEL_AUTHORITY = import.meta.env.VITE_ZITADEL_AUTHORITY ?? ''
export const ZITADEL_CLIENT_ID = import.meta.env.VITE_ZITADEL_CLIENT_ID ?? ''
export const ZITADEL_REDIRECT_URI =
  import.meta.env.VITE_ZITADEL_REDIRECT_URI ?? `${window.location.origin}/callback`
export const ZITADEL_POST_LOGOUT_REDIRECT_URI =
  import.meta.env.VITE_ZITADEL_POST_LOGOUT_REDIRECT_URI ?? window.location.origin
export const ZITADEL_SCOPES = ['openid', 'profile', 'email']

// Entra ID Configuration (legacy)
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
