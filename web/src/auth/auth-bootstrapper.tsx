import { useEffect } from 'react'
import { AUTH_MODE } from './auth-config'
import { bootstrapAuth } from './auth-service'
import { useAuthStore } from '@/stores/auth-store'

export function AuthBootstrapper() {
  const setAuthError = useAuthStore((state) => state.setAuthError)

  useEffect(() => {
    if (AUTH_MODE !== 'entra') {
      return
    }

    bootstrapAuth().catch(() => {
      setAuthError('Failed to initialize authentication.')
    })
  }, [setAuthError])

  return null
}
