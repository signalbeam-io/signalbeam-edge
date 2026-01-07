import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { zitadelAuth } from '@/auth/zitadel-service'
import { useAuthStore } from '@/stores/auth-store'
import { apiClient } from '@/lib/api-client'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { User as OidcUser } from 'oidc-client-ts'
import type { SubscriptionInfo, User, UserRole } from '@/stores/auth-store'

const registerSchema = z.object({
  tenantName: z
    .string()
    .min(2, 'Tenant name must be at least 2 characters')
    .max(100, 'Tenant name must be less than 100 characters'),
  tenantSlug: z
    .string()
    .min(2, 'Slug must be at least 2 characters')
    .max(63, 'Slug must be less than 63 characters')
    .regex(/^[a-z0-9][a-z0-9-]*[a-z0-9]$/, 'Slug must be lowercase letters, numbers, and hyphens'),
})

type RegisterFormData = z.infer<typeof registerSchema>

interface RegisterUserResponse {
  userId: string
  tenantId: string
  tenantName: string
  tenantSlug: string
  subscriptionTier: 'Free' | 'Paid'
}

export function RegisterPage() {
  const navigate = useNavigate()
  const setJwtAuth = useAuthStore((state) => state.setJwtAuth)
  const setAuthError = useAuthStore((state) => state.setAuthError)
  const [oidcUser, setOidcUser] = useState<OidcUser | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string>('')

  const {
    register,
    handleSubmit,
    formState: { errors },
    setValue,
    watch,
  } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
  })

  const tenantName = watch('tenantName')

  useEffect(() => {
    // Retrieve OIDC user from sessionStorage
    const storedUser = sessionStorage.getItem('oidc_user')
    if (!storedUser) {
      // No OIDC user found, redirect to login
      navigate('/login', { replace: true })
      return
    }

    try {
      const user = JSON.parse(storedUser) as OidcUser
      setOidcUser(user)
    } catch (error) {
      console.error('Failed to parse OIDC user:', error)
      navigate('/login', { replace: true })
    }
  }, [navigate])

  // Auto-generate slug from tenant name
  useEffect(() => {
    if (tenantName) {
      const slug = tenantName
        .toLowerCase()
        .replace(/[^a-z0-9-\s]/g, '')
        .replace(/\s+/g, '-')
        .replace(/^-+|-+$/g, '')
      setValue('tenantSlug', slug)
    }
  }, [tenantName, setValue])

  const onSubmit = async (data: RegisterFormData) => {
    if (!oidcUser) {
      setError('Authentication session expired. Please login again.')
      return
    }

    setIsSubmitting(true)
    setError('')

    try {
      // Register user with backend
      const response = await apiClient.post<RegisterUserResponse>('/api/auth/register', {
        email: oidcUser.profile.email,
        name: oidcUser.profile.name,
        zitadelUserId: oidcUser.profile.sub,
        tenantName: data.tenantName,
        tenantSlug: data.tenantSlug,
      }, {
        headers: {
          Authorization: `Bearer ${oidcUser.access_token}`,
        },
      })

      // Clear OIDC user from sessionStorage
      sessionStorage.removeItem('oidc_user')

      // Set auth state
      const userData = response.data
      const user: User = {
        id: userData.userId,
        email: oidcUser.profile.email ?? '',
        name: oidcUser.profile.name ?? '',
        tenantId: userData.tenantId,
        tenantName: userData.tenantName,
        tenantSlug: userData.tenantSlug,
        role: 'Admin' as UserRole, // First user is always admin
      }

      const subscription: SubscriptionInfo = {
        tier: userData.subscriptionTier,
        maxDevices: userData.subscriptionTier === 'Free' ? 5 : Number.MAX_SAFE_INTEGER,
        currentDeviceCount: 0,
        dataRetentionDays: userData.subscriptionTier === 'Free' ? 7 : 90,
      }

      setJwtAuth(
        user,
        oidcUser.access_token,
        oidcUser.expires_at ? new Date(oidcUser.expires_at * 1000).toISOString() : null,
        subscription
      )

      // Navigate to dashboard
      navigate('/dashboard', { replace: true })
    } catch (error: unknown) {
      console.error('Registration error:', error)
      const axiosError = error as { response?: { data?: { message?: string } } }
      setError(
        axiosError.response?.data?.message ??
          'Registration failed. Please try again or contact support.'
      )
      setAuthError('Registration failed')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleCancel = async () => {
    // Clear OIDC user and logout
    sessionStorage.removeItem('oidc_user')
    await zitadelAuth.logout()
    navigate('/login', { replace: true })
  }

  if (!oidcUser) {
    return null // Will redirect to login
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4 py-16">
      <div className="w-full max-w-md space-y-6 rounded-lg border bg-card p-8 shadow-sm">
        <div className="space-y-2 text-center">
          <h1 className="text-2xl font-semibold">Create Your Workspace</h1>
          <p className="text-sm text-muted-foreground">
            Welcome, {oidcUser.profile.name}! Let's set up your SignalBeam workspace.
          </p>
        </div>

        {error && (
          <div className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {error}
          </div>
        )}

        <form className="space-y-4" onSubmit={handleSubmit(onSubmit)}>
          <div className="space-y-2">
            <Label htmlFor="tenantName">Workspace Name</Label>
            <Input
              id="tenantName"
              placeholder="Acme Corporation"
              {...register('tenantName')}
              disabled={isSubmitting}
            />
            {errors.tenantName && (
              <p className="text-xs text-destructive">{errors.tenantName.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="tenantSlug">Workspace URL</Label>
            <div className="flex items-center gap-2">
              <span className="text-sm text-muted-foreground">signalbeam.io/</span>
              <Input
                id="tenantSlug"
                placeholder="acme-corp"
                {...register('tenantSlug')}
                disabled={isSubmitting}
                className="flex-1"
              />
            </div>
            {errors.tenantSlug && (
              <p className="text-xs text-destructive">{errors.tenantSlug.message}</p>
            )}
            <p className="text-xs text-muted-foreground">
              This will be your unique workspace identifier
            </p>
          </div>

          <div className="rounded-md border bg-muted/50 p-3 text-sm">
            <p className="font-medium">Your Free Tier includes:</p>
            <ul className="mt-2 space-y-1 text-muted-foreground">
              <li>• Up to 5 devices</li>
              <li>• 7 days data retention</li>
              <li>• Basic monitoring features</li>
            </ul>
          </div>

          <div className="flex gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={handleCancel}
              disabled={isSubmitting}
              className="flex-1"
            >
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} className="flex-1">
              {isSubmitting ? 'Creating...' : 'Create Workspace'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
