/**
 * Group memberships view component
 */

import { useState } from 'react'
import { X, Users, Info } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { useToast } from '@/hooks/use-toast'
import { useGroupMemberships, useRemoveDeviceFromGroup } from '@/hooks/api'
import { MembershipType } from '@/api/types'

export interface GroupMembershipsProps {
  groupId: string
  groupName: string
  isStaticGroup: boolean
}

export function GroupMemberships({ groupId, groupName, isStaticGroup }: GroupMembershipsProps) {
  const [deviceToRemove, setDeviceToRemove] = useState<{ id: string; name: string } | null>(null)
  const { toast } = useToast()

  // Fetch memberships
  const { data: membershipsData, isLoading, error } = useGroupMemberships(groupId)

  // Remove device mutation
  const removeDeviceMutation = useRemoveDeviceFromGroup()

  const handleRemoveDevice = async () => {
    if (!deviceToRemove) return

    try {
      await removeDeviceMutation.mutateAsync({
        groupId,
        deviceId: deviceToRemove.id,
      })

      toast({
        title: 'Device removed',
        description: `${deviceToRemove.name} has been removed from ${groupName}.`,
      })

      setDeviceToRemove(null)
    } catch (error) {
      toast({
        title: 'Error removing device',
        description: error instanceof Error ? error.message : 'Failed to remove device',
        variant: 'destructive',
      })
    }
  }

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Users className="h-5 w-5" />
            Group Memberships
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">Loading memberships...</p>
        </CardContent>
      </Card>
    )
  }

  if (error) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Users className="h-5 w-5" />
            Group Memberships
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Alert variant="destructive">
            <AlertDescription>
              Failed to load memberships: {error instanceof Error ? error.message : 'Unknown error'}
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    )
  }

  const memberships = membershipsData?.memberships || []
  const stats = {
    total: membershipsData?.totalMemberships || 0,
    static: membershipsData?.staticMemberships || 0,
    dynamic: membershipsData?.dynamicMemberships || 0,
  }

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-start justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <Users className="h-5 w-5" />
                Group Memberships
              </CardTitle>
              <CardDescription>
                {isStaticGroup
                  ? 'Manually managed device memberships'
                  : 'Automatically managed based on tag query'}
              </CardDescription>
            </div>
            <div className="flex gap-2">
              <div className="text-right">
                <p className="text-2xl font-bold">{stats.total}</p>
                <p className="text-xs text-muted-foreground">Total Devices</p>
              </div>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {/* Membership Stats */}
          {!isStaticGroup && stats.total > 0 && (
            <div className="mb-4 flex gap-4">
              <div className="flex items-center gap-2">
                <Badge variant="secondary">{stats.static}</Badge>
                <span className="text-sm text-muted-foreground">Static</span>
              </div>
              <div className="flex items-center gap-2">
                <Badge variant="default">{stats.dynamic}</Badge>
                <span className="text-sm text-muted-foreground">Dynamic</span>
              </div>
            </div>
          )}

          {/* Dynamic Group Info */}
          {!isStaticGroup && (
            <Alert className="mb-4">
              <Info className="h-4 w-4" />
              <AlertDescription>
                Dynamic memberships update automatically every 5 minutes based on device tags.
                You cannot manually remove dynamically added devices.
              </AlertDescription>
            </Alert>
          )}

          {/* Memberships Table */}
          {memberships.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No devices in this group yet.
              {isStaticGroup && ' Add devices to get started.'}
            </p>
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Device Name</TableHead>
                    <TableHead>Membership Type</TableHead>
                    <TableHead>Added At</TableHead>
                    <TableHead>Added By</TableHead>
                    {isStaticGroup && <TableHead className="w-[100px]">Actions</TableHead>}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {memberships.map((membership) => (
                    <TableRow key={membership.membershipId}>
                      <TableCell className="font-medium">
                        {membership.deviceName}
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant={
                            membership.type === MembershipType.Static
                              ? 'secondary'
                              : 'default'
                          }
                        >
                          {membership.type}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {new Date(membership.addedAt).toLocaleString()}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {membership.addedBy}
                      </TableCell>
                      {isStaticGroup && (
                        <TableCell>
                          {membership.type === MembershipType.Static && (
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() =>
                                setDeviceToRemove({
                                  id: membership.deviceId,
                                  name: membership.deviceName,
                                })
                              }
                              disabled={removeDeviceMutation.isPending}
                            >
                              <X className="h-4 w-4" />
                            </Button>
                          )}
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Remove Device Confirmation Dialog */}
      <AlertDialog open={!!deviceToRemove} onOpenChange={(open) => !open && setDeviceToRemove(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Remove Device from Group</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to remove <strong>{deviceToRemove?.name}</strong> from{' '}
              <strong>{groupName}</strong>? This action can be undone by adding the device back.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleRemoveDevice}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Remove Device
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
