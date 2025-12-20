import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { AUTH_MODE, ENTRA_CLIENT_ID } from '@/auth/auth-config'
import { handleAuthRedirect, loginWithApiKey, loginWithEntra } from '@/auth/auth-service'
import { useAuthStore } from '@/stores/auth-store'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

export function LoginPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const [apiKey, setApiKey] = useState('')
  const authError = useAuthStore((state) => state.authError)
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated)
  const setAuthError = useAuthStore((state) => state.setAuthError)

  const redirectPath = useMemo(
    () => searchParams.get('redirect') ?? '/dashboard',
    [searchParams]
  )

  useEffect(() => {
    let isMounted = true

    async function handleRedirect() {
      if (AUTH_MODE !== 'entra') {
        return
      }
      try {
        const redirected = await handleAuthRedirect()
        if (redirected && isMounted) {
          navigate(redirectPath, { replace: true })
        }
      } catch {
        if (isMounted) {
          setAuthError('Sign-in failed. Please try again.')
        }
      }
    }

    handleRedirect()

    return () => {
      isMounted = false
    }
  }, [navigate, redirectPath, setAuthError])

  useEffect(() => {
    if (isAuthenticated) {
      navigate(redirectPath, { replace: true })
    }
  }, [isAuthenticated, navigate, redirectPath])

  const handleApiKeySubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!apiKey.trim()) {
      setAuthError('API key is required.')
      return
    }
    loginWithApiKey(apiKey)
    navigate(redirectPath, { replace: true })
  }

  const handleEntraLogin = async () => {
    setAuthError(null)
    const redirectUrl = new URL(redirectPath, window.location.origin).toString()
    await loginWithEntra(redirectUrl)
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4 py-16">
      <div className="w-full max-w-md space-y-6 rounded-lg border bg-card p-8 shadow-sm">
        <div className="space-y-2 text-center">
          <h1 className="text-2xl font-semibold">Sign in to SignalBeam</h1>
          <p className="text-sm text-muted-foreground">
            Manage devices, bundles, and telemetry in one place.
          </p>
        </div>

        {authError && (
          <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {authError}
          </div>
        )}

        {AUTH_MODE === 'entra' ? (
          <div className="space-y-4">
            <Button
              className="w-full"
              onClick={handleEntraLogin}
              type="button"
              disabled={!ENTRA_CLIENT_ID}
            >
              Sign in with Microsoft
            </Button>
            {!ENTRA_CLIENT_ID && (
              <p className="text-xs text-muted-foreground">
                Configure <code className="font-mono">VITE_ENTRA_CLIENT_ID</code> to enable
                Entra sign-in.
              </p>
            )}
          </div>
        ) : (
          <form className="space-y-4" onSubmit={handleApiKeySubmit}>
            <div className="space-y-2">
              <label className="text-sm font-medium" htmlFor="apiKey">
                API Key
              </label>
              <Input
                id="apiKey"
                name="apiKey"
                placeholder="dev-api-key-1"
                value={apiKey}
                onChange={(event) => setApiKey(event.target.value)}
              />
            </div>
            <Button className="w-full" type="submit">
              Continue
            </Button>
          </form>
        )}
      </div>
    </div>
  )
}
