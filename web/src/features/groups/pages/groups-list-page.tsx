/**
 * Groups List Page - View and manage device groups
 */

import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Plus, RefreshCw, Users } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { useGroups, useCreateGroup } from '@/hooks/api'
import { GroupType } from '@/api/types'
import type { CreateGroupRequest, UpdateGroupRequest } from '@/api/types'
import { GroupForm } from '../components/group-form'
import { useToast } from '@/hooks/use-toast'

export function GroupsListPage() {
  const navigate = useNavigate()
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false)
  const { toast } = useToast()

  // Fetch groups
  const { data: groupsData, isLoading, refetch, isFetching } = useGroups()
  const groups = groupsData?.data || []

  // Create group mutation
  const createGroup = useCreateGroup()

  const handleCreateGroup = async (data: CreateGroupRequest | UpdateGroupRequest) => {
    try {
      // Since this is the create page, we know it's a CreateGroupRequest
      const newGroup = await createGroup.mutateAsync(data as CreateGroupRequest)

      toast({
        title: 'Group created',
        description: `${newGroup.name} has been created successfully.`,
      })

      setIsCreateDialogOpen(false)

      // Navigate to the new group's detail page
      navigate(`/groups/${newGroup.id}`)
    } catch (error) {
      toast({
        title: 'Failed to create group',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    }
  }

  const handleRefresh = () => {
    refetch()
  }

  const handleRowClick = (groupId: string) => {
    navigate(`/groups/${groupId}`)
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

  return (
    <div className="container mx-auto space-y-6 py-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Device Groups</h1>
          <p className="text-sm text-muted-foreground">
            Organize devices into static or dynamic groups
          </p>
        </div>
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
          <Button onClick={() => setIsCreateDialogOpen(true)}>
            <Plus className="mr-2 h-4 w-4" />
            Create Group
          </Button>
        </div>
      </div>

      {/* Groups Table */}
      <Card>
        <CardHeader>
          <CardTitle>All Groups</CardTitle>
          <CardDescription>
            {groups.length} group{groups.length !== 1 ? 's' : ''} total
          </CardDescription>
        </CardHeader>
        <CardContent>
          {groups.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12">
              <Users className="mb-4 h-12 w-12 text-muted-foreground" />
              <h3 className="mb-2 text-lg font-semibold">No Groups Yet</h3>
              <p className="mb-4 text-sm text-muted-foreground">
                Create your first group to organize devices
              </p>
              <Button onClick={() => setIsCreateDialogOpen(true)}>
                <Plus className="mr-2 h-4 w-4" />
                Create Group
              </Button>
            </div>
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Name</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead>Description</TableHead>
                    <TableHead>Devices</TableHead>
                    <TableHead>Query</TableHead>
                    <TableHead>Created</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {groups.map((group) => (
                    <TableRow
                      key={group.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => handleRowClick(group.id)}
                    >
                      <TableCell className="font-medium">{group.name}</TableCell>
                      <TableCell>
                        <Badge variant={group.type === GroupType.Static ? 'secondary' : 'default'}>
                          {group.type}
                        </Badge>
                      </TableCell>
                      <TableCell className="max-w-xs truncate text-muted-foreground">
                        {group.description || '—'}
                      </TableCell>
                      <TableCell>
                        <Badge variant="outline">{group.deviceIds.length}</Badge>
                      </TableCell>
                      <TableCell className="max-w-xs truncate text-sm font-mono">
                        {group.tagQuery || '—'}
                      </TableCell>
                      <TableCell className="text-muted-foreground">
                        {new Date(group.createdAt).toLocaleDateString()}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Create Group Dialog */}
      <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Create Device Group</DialogTitle>
            <DialogDescription>
              Create a new static or dynamic group to organize your devices
            </DialogDescription>
          </DialogHeader>
          <GroupForm
            onSubmit={handleCreateGroup}
            onCancel={() => setIsCreateDialogOpen(false)}
            isLoading={createGroup.isPending}
          />
        </DialogContent>
      </Dialog>
    </div>
  )
}
