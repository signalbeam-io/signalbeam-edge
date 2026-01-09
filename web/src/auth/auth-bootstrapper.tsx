import { useEffect, useRef } from 'react'
import { AUTH_MODE } from './auth-config'
import { bootstrapAuth } from './auth-service'
import { zitadelAuth } from './zitadel-service'
import { useAuthStore } from '@/stores/auth-store'

export function AuthBootstrapper() {
  const setAuthError = useAuthStore((state) => state.setAuthError)
  const hasInitialized = useRef(false)

  useEffect(() => {
    // Only run once on app initialization
    if (hasInitialized.current) return
    hasInitialized.current = true

    async function initializeAuth() {
      if (AUTH_MODE === 'zitadel') {
        // For Zitadel, just check if user exists in storage
        // Don't do any redirects here - let the pages handle that
        try {
          const user = await zitadelAuth.getUser()
          if (user && !user.expired) {
            console.log('Zitadel session restored for user:', user.profile.sub)
          } else if (user?.expired) {
            // Try to renew the token
            const renewedUser = await zitadelAuth.renewToken()
            if (!renewedUser) {
              // Token renewal failed, clear auth
              useAuthStore.getState().clearAuth()
            }
          }
        } catch (error) {
          console.error('Failed to initialize Zitadel authentication:', error)
          setAuthError('Failed to initialize authentication.')
        }
      } else if (AUTH_MODE === 'entra') {
        bootstrapAuth().catch(() => {
          setAuthError('Failed to initialize authentication.')
        })
      }
    }

    initializeAuth()
  }, [setAuthError])

  return null
}
