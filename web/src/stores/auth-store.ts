import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { AUTH_MODE, type AuthMode } from '@/auth/auth-config'

export type UserRole = 'Admin' | 'DeviceOwner'
export type SubscriptionTier = 'Free' | 'Paid'

export interface SubscriptionInfo {
  tier: SubscriptionTier
  maxDevices: number
  currentDeviceCount: number
  dataRetentionDays: number
}

export interface User {
  id: string
  email: string
  name: string
  // Tenant context (populated for Zitadel auth)
  tenantId?: string
  tenantName?: string
  tenantSlug?: string
  role?: UserRole
}

interface AuthState {
  authMode: AuthMode
  user: User | null
  subscription: SubscriptionInfo | null
  accessToken: string | null
  apiKey: string | null
  expiresAt: string | null
  isAuthenticated: boolean
  authError: string | null
  setJwtAuth: (
    user: User,
    accessToken: string,
    expiresAt: string | null,
    subscription?: SubscriptionInfo | null
  ) => void
  setApiKeyAuth: (apiKey: string, user?: User) => void
  setAuthError: (message: string | null) => void
  setSubscription: (subscription: SubscriptionInfo) => void
  clearAuth: () => void
}

/**
 * Authentication store using Zustand
 * Persisted to localStorage
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      authMode: AUTH_MODE,
      user: null,
      subscription: null,
      accessToken: null,
      apiKey: null,
      expiresAt: null,
      isAuthenticated: false,
      authError: null,
      setJwtAuth: (user, accessToken, expiresAt, subscription = null) =>
        set({
          user,
          accessToken,
          apiKey: null,
          expiresAt,
          subscription,
          isAuthenticated: true,
          authError: null,
        }),
      setApiKeyAuth: (apiKey, user) =>
        set({
          apiKey,
          user: user ?? { id: 'api-key', email: 'api-key', name: 'API Key User' },
          accessToken: null,
          expiresAt: null,
          subscription: null,
          isAuthenticated: true,
          authError: null,
        }),
      setAuthError: (message) => set({ authError: message }),
      setSubscription: (subscription) => set({ subscription }),
      clearAuth: () =>
        set({
          user: null,
          accessToken: null,
          apiKey: null,
          expiresAt: null,
          subscription: null,
          isAuthenticated: false,
          authError: null,
        }),
    }),
    {
      name: 'auth-storage',
      partialize: (state) => ({
        user: state.user,
        subscription: state.subscription,
        accessToken: state.accessToken,
        apiKey: state.apiKey,
        expiresAt: state.expiresAt,
        isAuthenticated: state.isAuthenticated,
      }),
    }
  )
)
