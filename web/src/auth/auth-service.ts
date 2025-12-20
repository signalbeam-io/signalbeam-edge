import {
  InteractionRequiredAuthError,
  type AuthenticationResult,
  PublicClientApplication,
} from '@azure/msal-browser'
import {
  AUTH_MODE,
  ENTRA_AUTHORITY,
  ENTRA_CLIENT_ID,
  ENTRA_POST_LOGOUT_REDIRECT_URI,
  ENTRA_REDIRECT_URI,
  ENTRA_SCOPES,
} from './auth-config'
import { useAuthStore, type User } from '@/stores/auth-store'

let msalInstance: PublicClientApplication | null = null
let msalInitialized = false

function getMsalInstance() {
  if (!msalInstance) {
    msalInstance = new PublicClientApplication({
      auth: {
        clientId: ENTRA_CLIENT_ID,
        authority: ENTRA_AUTHORITY,
        redirectUri: ENTRA_REDIRECT_URI,
      },
      cache: {
        cacheLocation: 'localStorage',
        storeAuthStateInCookie: false,
      },
    })
  }
  return msalInstance
}

async function ensureMsalInitialized() {
  if (msalInitialized) {
    return
  }
  await getMsalInstance().initialize()
  msalInitialized = true
}

function isTokenExpiring(expiresAt: string | null) {
  if (!expiresAt) {
    return true
  }
  const expiresAtMs = new Date(expiresAt).getTime()
  return Date.now() >= expiresAtMs - 60_000
}

function toUser(result: AuthenticationResult): User {
  const account = result.account
  return {
    id: account?.homeAccountId ?? 'entra-user',
    email: account?.username ?? 'unknown',
    name: account?.name ?? account?.username ?? 'Unknown User',
  }
}

function applyAuthResult(result: AuthenticationResult) {
  const expiresAt = result.expiresOn ? result.expiresOn.toISOString() : null
  useAuthStore.getState().setJwtAuth(toUser(result), result.accessToken, expiresAt)
}

export async function handleAuthRedirect(): Promise<boolean> {
  if (AUTH_MODE !== 'entra') {
    return false
  }
  await ensureMsalInitialized()
  const result = await getMsalInstance().handleRedirectPromise()
  if (result) {
    getMsalInstance().setActiveAccount(result.account)
    applyAuthResult(result)
    return true
  }
  return false
}

export async function bootstrapAuth(): Promise<void> {
  if (AUTH_MODE !== 'entra') {
    return
  }
  if (!ENTRA_CLIENT_ID) {
    useAuthStore.getState().setAuthError('Missing Entra client ID configuration.')
    return
  }
  await ensureMsalInitialized()
  const account = getMsalInstance().getActiveAccount() ?? getMsalInstance().getAllAccounts()[0]
  if (!account) {
    return
  }
  getMsalInstance().setActiveAccount(account)
  if (useAuthStore.getState().isAuthenticated) {
    return
  }
  try {
    const result = await getMsalInstance().acquireTokenSilent({ account, scopes: ENTRA_SCOPES })
    applyAuthResult(result)
  } catch (error) {
    if (!(error instanceof InteractionRequiredAuthError)) {
      useAuthStore.getState().setAuthError('Failed to restore session.')
    }
  }
}

export async function loginWithEntra(redirectPath?: string) {
  if (AUTH_MODE !== 'entra') {
    return
  }
  if (!ENTRA_CLIENT_ID) {
    useAuthStore.getState().setAuthError('Missing Entra client ID configuration.')
    return
  }
  await ensureMsalInitialized()
  await getMsalInstance().loginRedirect({
    scopes: ENTRA_SCOPES,
    redirectStartPage: redirectPath ?? window.location.href,
  })
}

export function loginWithApiKey(apiKey: string) {
  useAuthStore.getState().setApiKeyAuth(apiKey.trim())
}

export async function logout() {
  useAuthStore.getState().clearAuth()
  if (AUTH_MODE !== 'entra') {
    return
  }
  await ensureMsalInitialized()
  await getMsalInstance().logoutRedirect({
    postLogoutRedirectUri: ENTRA_POST_LOGOUT_REDIRECT_URI,
  })
}

export async function getAccessToken(): Promise<string | null> {
  if (AUTH_MODE !== 'entra') {
    return null
  }
  if (!ENTRA_CLIENT_ID) {
    useAuthStore.getState().setAuthError('Missing Entra client ID configuration.')
    return null
  }
  await ensureMsalInitialized()
  const account = getMsalInstance().getActiveAccount() ?? getMsalInstance().getAllAccounts()[0]
  if (!account) {
    return null
  }
  getMsalInstance().setActiveAccount(account)

  const store = useAuthStore.getState()
  if (store.accessToken && !isTokenExpiring(store.expiresAt)) {
    return store.accessToken
  }

  try {
    const result = await getMsalInstance().acquireTokenSilent({ account, scopes: ENTRA_SCOPES })
    applyAuthResult(result)
    return result.accessToken
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      useAuthStore.getState().setAuthError('Login required to continue.')
      return null
    }
    throw error
  }
}
