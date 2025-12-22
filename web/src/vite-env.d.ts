/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL: string
  readonly VITE_APP_ENV: string
  readonly VITE_ENABLE_DEVTOOLS: string
  readonly VITE_TENANT_ID: string
  readonly VITE_AUTH_MODE?: 'entra' | 'apiKey'
  readonly VITE_ENTRA_CLIENT_ID?: string
  readonly VITE_ENTRA_TENANT_ID?: string
  readonly VITE_ENTRA_AUTHORITY?: string
  readonly VITE_ENTRA_REDIRECT_URI?: string
  readonly VITE_ENTRA_POST_LOGOUT_REDIRECT_URI?: string
  readonly VITE_ENTRA_SCOPES?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
