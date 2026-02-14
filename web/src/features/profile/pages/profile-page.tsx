import { useState } from 'react'
import { useAuthStore } from '@/stores/auth-store'
import { ZITADEL_AUTHORITY } from '@/auth/auth-config'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Progress } from '@/components/ui/progress'
import { Separator } from '@/components/ui/separator'
import { EditProfileDialog } from '../components/edit-profile-dialog'
import { DeleteAccountDialog } from '../components/delete-account-dialog'

export function ProfilePage() {
  const user = useAuthStore((s) => s.user)
  const subscription = useAuthStore((s) => s.subscription)
  const [editOpen, setEditOpen] = useState(false)
  const [deleteOpen, setDeleteOpen] = useState(false)

  if (!user) return null

  const deviceUsagePercent = subscription
    ? Math.round((subscription.currentDeviceCount / subscription.maxDevices) * 100)
    : 0

  const changePasswordUrl = ZITADEL_AUTHORITY
    ? `${ZITADEL_AUTHORITY}/ui/console/users/me`
    : null

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Profile</h1>
        <p className="text-muted-foreground">Manage your account settings</p>
      </div>

      {/* Profile Info */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle>Profile Information</CardTitle>
              <CardDescription>Your personal details</CardDescription>
            </div>
            <Button variant="outline" size="sm" onClick={() => setEditOpen(true)}>
              Edit
            </Button>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <p className="text-sm font-medium text-muted-foreground">Name</p>
              <p className="text-sm">{user.name}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Email</p>
              <p className="text-sm">{user.email}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Role</p>
              <p className="text-sm">{user.role ?? 'N/A'}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Tenant</p>
              <p className="text-sm">{user.tenantName ?? 'N/A'}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Subscription */}
      {subscription && (
        <Card>
          <CardHeader>
            <CardTitle>Subscription</CardTitle>
            <CardDescription>Your current plan and usage</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium text-muted-foreground">Tier</span>
              <Badge variant={subscription.tier === 'Paid' ? 'default' : 'secondary'}>
                {subscription.tier}
              </Badge>
            </div>
            <div>
              <div className="mb-1.5 flex items-center justify-between text-sm">
                <span className="font-medium text-muted-foreground">Device Usage</span>
                <span>
                  {subscription.currentDeviceCount} / {subscription.maxDevices} devices
                </span>
              </div>
              <Progress value={deviceUsagePercent} />
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Data Retention</p>
              <p className="text-sm">{subscription.dataRetentionDays} days</p>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Account Actions */}
      <Card>
        <CardHeader>
          <CardTitle>Account</CardTitle>
          <CardDescription>Manage your account security and settings</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {changePasswordUrl && (
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium">Change Password</p>
                <p className="text-sm text-muted-foreground">
                  Update your password via the identity provider
                </p>
              </div>
              <Button variant="outline" size="sm" asChild>
                <a href={changePasswordUrl} target="_blank" rel="noopener noreferrer">
                  Change Password
                </a>
              </Button>
            </div>
          )}
          <Separator />
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-destructive">Delete Account</p>
              <p className="text-sm text-muted-foreground">
                Permanently delete your account and all associated data
              </p>
            </div>
            <Button variant="destructive" size="sm" onClick={() => setDeleteOpen(true)}>
              Delete Account
            </Button>
          </div>
        </CardContent>
      </Card>

      <EditProfileDialog open={editOpen} onOpenChange={setEditOpen} currentName={user.name} />
      <DeleteAccountDialog open={deleteOpen} onOpenChange={setDeleteOpen} />
    </div>
  )
}
