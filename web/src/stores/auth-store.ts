import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { AUTH_MODE, type AuthMode } from '@/auth/auth-config'

export interface User {
  id: string
  email: string
  name: string
}

interface AuthState {
  authMode: AuthMode
  user: User | null
  accessToken: string | null
  apiKey: string | null
  expiresAt: string | null
  isAuthenticated: boolean
  authError: string | null
  setJwtAuth: (user: User, accessToken: string, expiresAt: string | null) => void
  setApiKeyAuth: (apiKey: string, user?: User) => void
  setAuthError: (message: string | null) => void
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
      accessToken: null,
      apiKey: null,
      expiresAt: null,
      isAuthenticated: false,
      authError: null,
      setJwtAuth: (user, accessToken, expiresAt) =>
        set({
          user,
          accessToken,
          apiKey: null,
          expiresAt,
          isAuthenticated: true,
          authError: null,
        }),
      setApiKeyAuth: (apiKey, user) =>
        set({
          apiKey,
          user: user ?? { id: 'api-key', email: 'api-key', name: 'API Key User' },
          accessToken: null,
          expiresAt: null,
          isAuthenticated: true,
          authError: null,
        }),
      setAuthError: (message) => set({ authError: message }),
      clearAuth: () =>
        set({
          user: null,
          accessToken: null,
          apiKey: null,
          expiresAt: null,
          isAuthenticated: false,
          authError: null,
        }),
    }),
    {
      name: 'auth-storage',
      partialize: (state) => ({
        user: state.user,
        accessToken: state.accessToken,
        apiKey: state.apiKey,
        expiresAt: state.expiresAt,
        isAuthenticated: state.isAuthenticated,
      }),
    }
  )
)
