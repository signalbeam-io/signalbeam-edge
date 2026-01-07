import { useEffect } from 'react'
import { AUTH_MODE } from './auth-config'
import { bootstrapAuth } from './auth-service'
import { zitadelAuth } from './zitadel-service'
import { useAuthStore } from '@/stores/auth-store'

export function AuthBootstrapper() {
  const setAuthError = useAuthStore((state) => state.setAuthError)

  useEffect(() => {
    async function initializeAuth() {
      if (AUTH_MODE === 'zitadel') {
        // For Zitadel, check if user exists in storage and validate
        try {
          const user = await zitadelAuth.getUser()
          if (user && !user.expired) {
            // User is still valid, auth store should already have the token
            // Zitadel's UserManager will handle automatic silent renewal
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
