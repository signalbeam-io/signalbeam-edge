/**
 * Bulk operations component for device groups
 */

import { useState } from 'react'
import { Loader2, Tag, Plus, Minus, CheckCircle, XCircle, AlertCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Alert, AlertDescription } from '@/components/ui/alert'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from '@/components/ui/tabs'
import { useToast } from '@/hooks/use-toast'
import { useBulkAddTags, useBulkRemoveTags, useTags } from '@/hooks/api'
import type { BulkOperationResponse } from '@/api/types'

export interface GroupBulkOperationsProps {
  groupId: string
  groupName: string
  deviceCount: number
}

type OperationType = 'add' | 'remove'

export function GroupBulkOperations({ groupId, groupName, deviceCount }: GroupBulkOperationsProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [operationType, setOperationType] = useState<OperationType>('add')
  const [tagInput, setTagInput] = useState('')
  const [operationResult, setOperationResult] = useState<BulkOperationResponse | null>(null)
  const { toast } = useToast()

  // Fetch all tags for suggestions
  const { data: tagsData } = useTags()
  const allTags = tagsData?.tags.map((t) => t.tag) || []

  // Mutations
  const bulkAddMutation = useBulkAddTags()
  const bulkRemoveMutation = useBulkRemoveTags()

  const isLoading = bulkAddMutation.isPending || bulkRemoveMutation.isPending

  // Validate tag format
  const validateTag = (tag: string): string | null => {
    if (!tag.trim()) {
      return 'Tag cannot be empty'
    }
    if (!/^[a-zA-Z0-9_\-=]+$/.test(tag)) {
      return 'Tag can only contain letters, numbers, hyphens, underscores, and ='
    }
    return null
  }

  const handleExecute = async () => {
    const trimmedTag = tagInput.trim()

    // Validate
    const error = validateTag(trimmedTag)
    if (error) {
      toast({
        title: 'Invalid tag',
        description: error,
        variant: 'destructive',
      })
      return
    }

    try {
      let result: BulkOperationResponse

      if (operationType === 'add') {
        result = await bulkAddMutation.mutateAsync({
          groupId,
          tag: trimmedTag,
        })
      } else {
        result = await bulkRemoveMutation.mutateAsync({
          groupId,
          tag: trimmedTag,
        })
      }

      setOperationResult(result)

      toast({
        title: 'Bulk operation completed',
        description: `${operationType === 'add' ? 'Added' : 'Removed'} tag "${trimmedTag}" ${
          operationType === 'add' ? 'to' : 'from'
        } ${result.devicesUpdated} device${result.devicesUpdated !== 1 ? 's' : ''}.`,
      })

      // Reset form
      setTagInput('')
    } catch (error) {
      toast({
        title: 'Bulk operation failed',
        description: error instanceof Error ? error.message : 'Failed to execute bulk operation',
        variant: 'destructive',
      })
    }
  }

  const handleClose = () => {
    setIsOpen(false)
    setTagInput('')
    setOperationResult(null)
    setOperationType('add')
  }

  const filteredSuggestions = allTags.filter((tag) =>
    tag.toLowerCase().includes(tagInput.toLowerCase())
  )

  return (
    <Dialog open={isOpen} onOpenChange={(open) => (open ? setIsOpen(true) : handleClose())}>
      <DialogTrigger asChild>
        <Button variant="outline">
          <Tag className="mr-2 h-4 w-4" />
          Bulk Operations
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-[600px]">
        <DialogHeader>
          <DialogTitle>Bulk Tag Operations</DialogTitle>
          <DialogDescription>
            Apply tag operations to all {deviceCount} device{deviceCount !== 1 ? 's' : ''} in{' '}
            <strong>{groupName}</strong>
          </DialogDescription>
        </DialogHeader>

        <Tabs value={operationType} onValueChange={(value) => setOperationType(value as OperationType)}>
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="add">
              <Plus className="mr-2 h-4 w-4" />
              Add Tag
            </TabsTrigger>
            <TabsTrigger value="remove">
              <Minus className="mr-2 h-4 w-4" />
              Remove Tag
            </TabsTrigger>
          </TabsList>

          <TabsContent value="add" className="space-y-4">
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                This will add the specified tag to all {deviceCount} devices in this group.
              </AlertDescription>
            </Alert>

            <div className="space-y-2">
              <Label htmlFor="add-tag">Tag to Add</Label>
              <Input
                id="add-tag"
                placeholder="e.g., environment=production"
                value={tagInput}
                onChange={(e) => setTagInput(e.target.value)}
                disabled={isLoading}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault()
                    handleExecute()
                  }
                }}
              />
              <p className="text-xs text-muted-foreground">
                Use key=value format or simple tags
              </p>

              {/* Suggestions */}
              {tagInput && filteredSuggestions.length > 0 && (
                <div className="rounded-md border p-2">
                  <p className="mb-2 text-xs font-medium text-muted-foreground">
                    Suggestions:
                  </p>
                  <div className="flex flex-wrap gap-1">
                    {filteredSuggestions.slice(0, 10).map((tag) => (
                      <Button
                        key={tag}
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => setTagInput(tag)}
                        className="h-7 text-xs"
                      >
                        {tag}
                      </Button>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </TabsContent>

          <TabsContent value="remove" className="space-y-4">
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                This will remove the specified tag from all {deviceCount} devices in this group that have it.
              </AlertDescription>
            </Alert>

            <div className="space-y-2">
              <Label htmlFor="remove-tag">Tag to Remove</Label>
              <Input
                id="remove-tag"
                placeholder="e.g., environment=staging"
                value={tagInput}
                onChange={(e) => setTagInput(e.target.value)}
                disabled={isLoading}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault()
                    handleExecute()
                  }
                }}
              />

              {/* Suggestions */}
              {tagInput && filteredSuggestions.length > 0 && (
                <div className="rounded-md border p-2">
                  <p className="mb-2 text-xs font-medium text-muted-foreground">
                    Suggestions:
                  </p>
                  <div className="flex flex-wrap gap-1">
                    {filteredSuggestions.slice(0, 10).map((tag) => (
                      <Button
                        key={tag}
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => setTagInput(tag)}
                        className="h-7 text-xs"
                      >
                        {tag}
                      </Button>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </TabsContent>
        </Tabs>

        {/* Operation Result */}
        {operationResult && (
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Operation Result</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              <div className="flex items-center gap-2">
                <CheckCircle className="h-4 w-4 text-green-600" />
                <span className="text-sm">
                  <strong>{operationResult.devicesUpdated}</strong> device
                  {operationResult.devicesUpdated !== 1 ? 's' : ''} updated successfully
                </span>
              </div>

              {operationResult.failedDeviceIds.length > 0 && (
                <div className="flex items-start gap-2">
                  <XCircle className="h-4 w-4 text-destructive" />
                  <div className="flex-1">
                    <span className="text-sm">
                      <strong>{operationResult.failedDeviceIds.length}</strong> device
                      {operationResult.failedDeviceIds.length !== 1 ? 's' : ''} failed
                    </span>
                    {operationResult.errors.length > 0 && (
                      <ul className="mt-1 list-inside list-disc text-xs text-muted-foreground">
                        {operationResult.errors.slice(0, 3).map((error, index) => (
                          <li key={index}>{error}</li>
                        ))}
                        {operationResult.errors.length > 3 && (
                          <li>...and {operationResult.errors.length - 3} more</li>
                        )}
                      </ul>
                    )}
                  </div>
                </div>
              )}
            </CardContent>
          </Card>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={isLoading}>
            {operationResult ? 'Close' : 'Cancel'}
          </Button>
          {!operationResult && (
            <Button onClick={handleExecute} disabled={isLoading || !tagInput.trim()}>
              {isLoading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Execute
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
