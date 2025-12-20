/**
 * Device Overview Tab - Shows tags, groups, bundle, and metadata
 */

import { useState } from 'react'
import { format } from 'date-fns'
import { Edit, Plus, X } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Device } from '@/api/types'
import { useUpdateDevice } from '@/hooks/api/use-devices'

interface DeviceOverviewTabProps {
  device: Device
}

export function DeviceOverviewTab({ device }: DeviceOverviewTabProps) {
  const [isEditingTags, setIsEditingTags] = useState(false)
  const [newTag, setNewTag] = useState('')
  const [tags, setTags] = useState<string[]>(device.tags || [])
  const updateDevice = useUpdateDevice()

  const handleAddTag = () => {
    if (newTag.trim() && !tags.includes(newTag.trim())) {
      const updatedTags = [...tags, newTag.trim()]
      setTags(updatedTags)
      setNewTag('')
      saveTagsToServer(updatedTags)
    }
  }

  const handleRemoveTag = (tagToRemove: string) => {
    const updatedTags = tags.filter((t) => t !== tagToRemove)
    setTags(updatedTags)
    saveTagsToServer(updatedTags)
  }

  const saveTagsToServer = async (updatedTags: string[]) => {
    try {
      await updateDevice.mutateAsync({
        id: device.id,
        data: { tags: updatedTags },
      })
    } catch (error) {
      console.error('Failed to update tags:', error)
      setTags(device.tags)
    }
  }

  return (
    <div className="grid gap-6 md:grid-cols-2">
      {/* Tags */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-4">
          <CardTitle className="text-base font-semibold">Tags</CardTitle>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setIsEditingTags(!isEditingTags)}
          >
            <Edit className="h-4 w-4" />
          </Button>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-2">
            {tags.length > 0 ? (
              tags.map((tag) => (
                <Badge key={tag} variant="secondary">
                  {tag}
                  {isEditingTags && (
                    <button
                      onClick={() => handleRemoveTag(tag)}
                      className="ml-1 hover:text-destructive"
                    >
                      <X className="h-3 w-3" />
                    </button>
                  )}
                </Badge>
              ))
            ) : (
              <p className="text-sm text-muted-foreground">No tags assigned</p>
            )}
          </div>
          {isEditingTags && (
            <div className="mt-4 flex gap-2">
              <Input
                placeholder="Add a tag"
                value={newTag}
                onChange={(e) => setNewTag(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault()
                    handleAddTag()
                  }
                }}
              />
              <Button onClick={handleAddTag} size="sm">
                <Plus className="h-4 w-4" />
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Groups */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-semibold">Groups</CardTitle>
        </CardHeader>
        <CardContent>
          {device.groupIds && device.groupIds.length > 0 ? (
            <div className="flex flex-wrap gap-2">
              {device.groupIds.map((groupId) => (
                <Badge key={groupId} variant="outline">
                  {groupId}
                </Badge>
              ))}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">Not part of any group</p>
          )}
        </CardContent>
      </Card>

      {/* Current Bundle */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-semibold">Current Bundle</CardTitle>
        </CardHeader>
        <CardContent>
          {device.currentBundleId ? (
            <div className="space-y-2">
              <div>
                <p className="text-sm font-medium text-muted-foreground">Bundle ID</p>
                <p className="text-sm font-semibold">{device.currentBundleId}</p>
              </div>
              {device.currentBundleVersion && (
                <div>
                  <p className="text-sm font-medium text-muted-foreground">Version</p>
                  <p className="text-sm font-semibold">
                    {device.currentBundleVersion}
                  </p>
                </div>
              )}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">No bundle assigned</p>
          )}
        </CardContent>
      </Card>

      {/* Device Information */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base font-semibold">
            Device Information
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div>
            <p className="text-sm font-medium text-muted-foreground">Created</p>
            <p className="text-sm">
              {format(new Date(device.createdAt), 'PPpp')}
            </p>
          </div>
          <div>
            <p className="text-sm font-medium text-muted-foreground">Last Updated</p>
            <p className="text-sm">
              {format(new Date(device.updatedAt), 'PPpp')}
            </p>
          </div>
        </CardContent>
      </Card>

      {/* Metadata */}
      {device.metadata && Object.keys(device.metadata).length > 0 && (
        <Card className="md:col-span-2">
          <CardHeader>
            <CardTitle className="text-base font-semibold">Metadata</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {Object.entries(device.metadata).map(([key, value]) => (
                <div key={key}>
                  <p className="text-sm font-medium text-muted-foreground">{key}</p>
                  <p className="text-sm font-semibold">
                    {typeof value === 'object'
                      ? JSON.stringify(value)
                      : String(value)}
                  </p>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
