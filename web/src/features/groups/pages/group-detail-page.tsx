/**
 * Group Detail Page - View and manage a specific device group
 */

import { useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, Edit2, Trash2, RefreshCw, Package } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Skeleton } from '@/components/ui/skeleton'
import { useToast } from '@/hooks/use-toast'
import { useGroup, useUpdateGroup, useDeleteGroup } from '@/hooks/api'
import { GroupType } from '@/api/types'
import type { UpdateGroupRequest } from '@/api/types'
import { GroupMemberships } from '../components/group-memberships'
import { GroupBulkOperations } from '../components/group-bulk-operations'
import { GroupAssignBundleDialog } from '../components/group-assign-bundle-dialog'
import { GroupForm } from '../components/group-form'

export function GroupDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { toast } = useToast()

  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false)
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false)

  // Fetch group data
  const { data: group, isLoading, refetch, isFetching } = useGroup(id || '', !!id)

  // Mutations
  const updateGroup = useUpdateGroup()
  const deleteGroup = useDeleteGroup()

  const handleUpdate = async (data: UpdateGroupRequest) => {
    if (!id) return

    try {
      await updateGroup.mutateAsync({ id, data })
      toast({
        title: 'Group updated',
        description: `${data.name || group?.name} has been updated successfully.`,
      })
      setIsEditDialogOpen(false)
    } catch (error) {
      toast({
        title: 'Failed to update group',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    }
  }

  const handleDelete = async () => {
    if (!id) return

    try {
      await deleteGroup.mutateAsync(id)
      toast({
        title: 'Group deleted',
        description: `${group?.name} has been deleted successfully.`,
      })
      navigate('/groups')
    } catch (error) {
      toast({
        title: 'Failed to delete group',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    }
  }

  const handleRefresh = () => {
    refetch()
  }

  // Loading state
  if (isLoading) {
    return (
      <div className="container mx-auto space-y-6 py-6">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-96 w-full" />
      </div>
    )
  }

  // Not found state
  if (!group) {
    return (
      <div className="container mx-auto py-6">
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <p className="text-lg font-medium">Group not found</p>
            <Button onClick={() => navigate('/groups')} className="mt-4">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back to Groups
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  const isStaticGroup = group.type === GroupType.Static
  const deviceCount = group.deviceIds.length

  return (
    <div className="container mx-auto space-y-6 py-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div className="space-y-1">
          {/* Breadcrumb */}
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate('/groups')}
            className="mb-2 -ml-2"
          >
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back to Groups
          </Button>

          {/* Title */}
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-bold tracking-tight">{group.name}</h1>
            <Badge variant={isStaticGroup ? 'secondary' : 'default'}>
              {group.type}
            </Badge>
          </div>

          {/* Description */}
          {group.description && (
            <p className="text-sm text-muted-foreground">{group.description}</p>
          )}

          {/* Tag Query (for dynamic groups) */}
          {!isStaticGroup && group.tagQuery && (
            <div className="mt-2">
              <p className="text-xs font-medium text-muted-foreground">Tag Query:</p>
              <code className="text-sm font-mono">{group.tagQuery}</code>
            </div>
          )}
        </div>

        {/* Action Buttons */}
        <div className="flex gap-2">
          <Button
            onClick={handleRefresh}
            variant="outline"
            size="sm"
            disabled={isFetching}
          >
            <RefreshCw className={`mr-2 h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
          <Button onClick={() => setIsEditDialogOpen(true)} variant="outline" size="sm">
            <Edit2 className="mr-2 h-4 w-4" />
            Edit
          </Button>
          <Button
            onClick={() => setIsDeleteDialogOpen(true)}
            variant="outline"
            size="sm"
            className="text-destructive hover:text-destructive"
          >
            <Trash2 className="mr-2 h-4 w-4" />
            Delete
          </Button>
        </div>
      </div>

      {/* Group Stats Card */}
      <Card>
        <CardHeader>
          <CardTitle>Group Information</CardTitle>
          <CardDescription>Overview of group configuration and membership</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-3">
            <div>
              <p className="text-sm font-medium text-muted-foreground">Type</p>
              <p className="text-2xl font-bold">{group.type}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Total Devices</p>
              <p className="text-2xl font-bold">{deviceCount}</p>
            </div>
            <div>
              <p className="text-sm font-medium text-muted-foreground">Created</p>
              <p className="text-2xl font-bold">
                {new Date(group.createdAt).toLocaleDateString()}
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Actions Card */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Package className="h-5 w-5" />
            Group Actions
          </CardTitle>
          <CardDescription>
            Perform bulk operations on all devices in this group
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-2">
            <GroupAssignBundleDialog
              groupId={group.id}
              groupName={group.name}
              deviceCount={deviceCount}
            />
            <GroupBulkOperations
              groupId={group.id}
              groupName={group.name}
              deviceCount={deviceCount}
            />
          </div>
        </CardContent>
      </Card>

      {/* Group Memberships */}
      <GroupMemberships
        groupId={group.id}
        groupName={group.name}
        isStaticGroup={isStaticGroup}
      />

      {/* Edit Group Dialog */}
      <Dialog open={isEditDialogOpen} onOpenChange={setIsEditDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Edit Group</DialogTitle>
            <DialogDescription>
              Update the group's name, description, or tag query
            </DialogDescription>
          </DialogHeader>
          <GroupForm
            group={group}
            onSubmit={handleUpdate}
            onCancel={() => setIsEditDialogOpen(false)}
            isLoading={updateGroup.isPending}
          />
        </DialogContent>
      </Dialog>

      {/* Delete Group Dialog */}
      <AlertDialog open={isDeleteDialogOpen} onOpenChange={setIsDeleteDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Group</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete <strong>{group.name}</strong>? This action
              cannot be undone.
              {deviceCount > 0 && (
                <>
                  <br />
                  <br />
                  This group currently has {deviceCount} device
                  {deviceCount !== 1 ? 's' : ''}. Deleting the group will not affect the
                  devices themselves, but they will no longer be part of this group.
                </>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Delete Group
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
