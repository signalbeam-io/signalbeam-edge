/**
 * Tag manager component for managing device tags
 */

import { useState } from 'react'
import { Plus, Tag as TagIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useToast } from '@/hooks/use-toast'
import { TagInput } from './tag-input'
import { useAddDeviceTag, useRemoveDeviceTag, useTags } from '@/hooks/api'

export interface TagManagerProps {
  deviceId: string
  currentTags: string[]
}

export function TagManager({ deviceId, currentTags }: TagManagerProps) {
  const [isAdding, setIsAdding] = useState(false)
  const { toast } = useToast()

  // Fetch all tags for suggestions
  const { data: tagsData } = useTags()
  const allTags = tagsData?.tags.map((t) => t.tag) || []

  // Mutations
  const addTagMutation = useAddDeviceTag()
  const removeTagMutation = useRemoveDeviceTag()

  // Validate tag format (key=value or simple tag)
  const validateTag = (tag: string): boolean | string => {
    if (!tag.trim()) {
      return 'Tag cannot be empty'
    }

    // Check for invalid characters (basic validation)
    if (!/^[a-zA-Z0-9_\-=]+$/.test(tag)) {
      return 'Tag can only contain letters, numbers, hyphens, underscores, and ='
    }

    return true
  }

  // Handle adding a tag
  const handleAddTag = async (tag: string) => {
    try {
      await addTagMutation.mutateAsync({
        deviceId,
        tag,
      })

      toast({
        title: 'Tag added',
        description: `Tag "${tag}" has been added to the device.`,
      })

      setIsAdding(false)
    } catch (error) {
      toast({
        title: 'Error adding tag',
        description: error instanceof Error ? error.message : 'Failed to add tag',
        variant: 'destructive',
      })
    }
  }

  // Handle removing a tag
  const handleRemoveTag = async (tag: string) => {
    try {
      await removeTagMutation.mutateAsync({
        deviceId,
        tag,
      })

      toast({
        title: 'Tag removed',
        description: `Tag "${tag}" has been removed from the device.`,
      })
    } catch (error) {
      toast({
        title: 'Error removing tag',
        description: error instanceof Error ? error.message : 'Failed to remove tag',
        variant: 'destructive',
      })
    }
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <TagIcon className="h-5 w-5" />
              Device Tags
            </CardTitle>
            <CardDescription>
              Manage tags for this device. Use key=value format for structured tags.
            </CardDescription>
          </div>
          {!isAdding && (
            <Button onClick={() => setIsAdding(true)} size="sm">
              <Plus className="mr-2 h-4 w-4" />
              Add Tag
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent>
        {isAdding && (
          <div className="mb-4">
            <TagInput
              value={[]}
              onChange={(tags) => {
                if (tags.length > 0) {
                  const tag = tags[0]
                  if (tag) {
                    handleAddTag(tag)
                  }
                }
              }}
              suggestions={allTags}
              placeholder="Enter tag (e.g., environment=production)"
              validateTag={validateTag}
              maxTags={1}
            />
            <div className="mt-2 flex gap-2">
              <Button
                size="sm"
                variant="outline"
                onClick={() => setIsAdding(false)}
              >
                Cancel
              </Button>
            </div>
          </div>
        )}

        {currentTags.length === 0 && !isAdding ? (
          <p className="text-sm text-muted-foreground">
            No tags assigned. Click "Add Tag" to add one.
          </p>
        ) : (
          <TagInput
            value={currentTags}
            onChange={async (newTags) => {
              // Find removed tag
              const removedTag = currentTags.find((tag) => !newTags.includes(tag))
              if (removedTag) {
                await handleRemoveTag(removedTag)
              }
            }}
            suggestions={[]}
            placeholder=""
            className="pointer-events-auto"
          />
        )}

        {currentTags.length > 0 && (
          <div className="mt-3 text-xs text-muted-foreground">
            <p>Examples:</p>
            <ul className="mt-1 list-inside list-disc space-y-0.5">
              <li>
                <code className="rounded bg-muted px-1">environment=production</code> - Key-value tag
              </li>
              <li>
                <code className="rounded bg-muted px-1">location=warehouse-1</code> - Structured tag
              </li>
              <li>
                <code className="rounded bg-muted px-1">critical</code> - Simple tag
              </li>
            </ul>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
