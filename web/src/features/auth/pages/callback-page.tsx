import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { zitadelAuth } from '@/auth/zitadel-service'
import { useAuthStore } from '@/stores/auth-store'
import { apiClient } from '@/lib/api-client'
import type { User as OidcUser } from 'oidc-client-ts'
import type { SubscriptionInfo, User, UserRole } from '@/stores/auth-store'

interface CurrentUserResponse {
  userId: string
  tenantId: string
  tenantName: string
  tenantSlug: string
  email: string
  name: string
  role: UserRole
  subscriptionTier: 'Free' | 'Paid'
  maxDevices: number
  currentDeviceCount: number
  dataRetentionDays: number
}

export function CallbackPage() {
  const navigate = useNavigate()
  const setJwtAuth = useAuthStore((state) => state.setJwtAuth)
  const setAuthError = useAuthStore((state) => state.setAuthError)
  const [status, setStatus] = useState<'loading' | 'checking' | 'registering' | 'error'>('loading')
  const [errorMessage, setErrorMessage] = useState<string>('')

  useEffect(() => {
    let isMounted = true

    async function handleCallback() {
      try {
        // Step 1: Handle OIDC callback
        setStatus('loading')
        const oidcUser: OidcUser = await zitadelAuth.handleCallback()

        if (!isMounted) return

        // Step 2: Check if user exists in our system
        setStatus('checking')
        try {
          const response = await apiClient.get<CurrentUserResponse>('/api/auth/me', {
            headers: {
              Authorization: `Bearer ${oidcUser.access_token}`,
            },
          })

          if (!isMounted) return

          // Step 3a: User exists - set auth state and navigate to dashboard
          const userData = response.data
          const user: User = {
            id: userData.userId,
            email: userData.email,
            name: userData.name,
            tenantId: userData.tenantId,
            tenantName: userData.tenantName,
            tenantSlug: userData.tenantSlug,
            role: userData.role,
          }

          const subscription: SubscriptionInfo = {
            tier: userData.subscriptionTier,
            maxDevices: userData.maxDevices,
            currentDeviceCount: userData.currentDeviceCount,
            dataRetentionDays: userData.dataRetentionDays,
          }

          setJwtAuth(
            user,
            oidcUser.access_token,
            oidcUser.expires_at ? new Date(oidcUser.expires_at * 1000).toISOString() : null,
            subscription
          )

          navigate('/dashboard', { replace: true })
        } catch (error: unknown) {
          if (!isMounted) return

          // Step 3b: User doesn't exist - navigate to registration
          const axiosError = error as { response?: { status?: number } }
          if (axiosError.response?.status === 404) {
            // Store OIDC user in sessionStorage for registration page
            sessionStorage.setItem('oidc_user', JSON.stringify(oidcUser))
            navigate('/register', { replace: true })
          } else {
            throw error
          }
        }
      } catch (error) {
        console.error('Callback error:', error)
        if (isMounted) {
          setStatus('error')
          setErrorMessage('Authentication failed. Please try again.')
          setAuthError('Authentication failed')
          setTimeout(() => {
            navigate('/login', { replace: true })
          }, 3000)
        }
      }
    }

    handleCallback()

    return () => {
      isMounted = false
    }
  }, [navigate, setJwtAuth, setAuthError])

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <div className="w-full max-w-md space-y-6 rounded-lg border bg-card p-8 shadow-sm">
        <div className="space-y-4 text-center">
          {status === 'loading' && (
            <>
              <div className="mx-auto h-12 w-12 animate-spin rounded-full border-4 border-primary border-t-transparent" />
              <h2 className="text-xl font-semibold">Processing authentication...</h2>
              <p className="text-sm text-muted-foreground">Please wait while we sign you in.</p>
            </>
          )}

          {status === 'checking' && (
            <>
              <div className="mx-auto h-12 w-12 animate-spin rounded-full border-4 border-primary border-t-transparent" />
              <h2 className="text-xl font-semibold">Verifying account...</h2>
              <p className="text-sm text-muted-foreground">Checking your account details.</p>
            </>
          )}

          {status === 'registering' && (
            <>
              <div className="mx-auto h-12 w-12 animate-spin rounded-full border-4 border-primary border-t-transparent" />
              <h2 className="text-xl font-semibold">Setting up your account...</h2>
              <p className="text-sm text-muted-foreground">This will only take a moment.</p>
            </>
          )}

          {status === 'error' && (
            <>
              <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-destructive/10">
                <svg
                  className="h-6 w-6 text-destructive"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M6 18L18 6M6 6l12 12"
                  />
                </svg>
              </div>
              <h2 className="text-xl font-semibold">Authentication Failed</h2>
              <p className="text-sm text-muted-foreground">{errorMessage}</p>
              <p className="text-xs text-muted-foreground">Redirecting to login...</p>
            </>
          )}
        </div>
      </div>
    </div>
  )
}
