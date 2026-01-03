/**
 * Group form component for creating and editing device groups
 */

import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { Loader2, HelpCircle, AlertCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Card, CardDescription, CardHeader } from '@/components/ui/card'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import { GroupType } from '@/api/types'
import type { DeviceGroup, CreateGroupRequest, UpdateGroupRequest } from '@/api/types'
import { cn } from '@/lib/utils'

export interface GroupFormProps {
  group?: DeviceGroup
  onSubmit: (data: CreateGroupRequest | UpdateGroupRequest) => Promise<void>
  onCancel?: () => void
  isLoading?: boolean
  error?: string | null
}

interface GroupFormData {
  name: string
  description: string
  type: GroupType
  tagQuery: string
}

export function GroupForm({
  group,
  onSubmit,
  onCancel,
  isLoading = false,
  error,
}: GroupFormProps) {
  const isEditMode = !!group

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isDirty },
  } = useForm<GroupFormData>({
    defaultValues: {
      name: group?.name || '',
      description: group?.description || '',
      type: group?.type || GroupType.Static,
      tagQuery: group?.tagQuery || '',
    },
  })

  const groupType = watch('type')
  const tagQuery = watch('tagQuery')

  // Clear tag query when switching to Static
  useEffect(() => {
    if (groupType === GroupType.Static) {
      setValue('tagQuery', '', { shouldDirty: true })
    }
  }, [groupType, setValue])

  const handleFormSubmit = async (data: GroupFormData) => {
    const trimmedDescription = data.description.trim()
    const trimmedTagQuery = data.type === GroupType.Dynamic ? data.tagQuery.trim() : ''

    const payload = {
      name: data.name.trim(),
      ...(trimmedDescription && { description: trimmedDescription }),
      type: data.type,
      ...(trimmedTagQuery && { tagQuery: trimmedTagQuery }),
    }

    await onSubmit(payload)
  }

  const exampleQueries = [
    {
      query: 'environment=production',
      description: 'Simple match',
    },
    {
      query: 'location=warehouse-*',
      description: 'Wildcard pattern',
    },
    {
      query: 'environment=production AND location=warehouse-1',
      description: 'AND operator',
    },
    {
      query: 'hardware=rpi4 OR hardware=rpi5',
      description: 'OR operator',
    },
    {
      query: 'NOT environment=dev',
      description: 'NOT operator',
    },
    {
      query: '(environment=production OR environment=staging) AND location=warehouse-*',
      description: 'Complex query',
    },
  ]

  return (
    <form onSubmit={handleSubmit(handleFormSubmit)}>
      <div className="space-y-6">
        {/* Group Name */}
        <div className="space-y-2">
          <Label htmlFor="name">
            Group Name <span className="text-destructive">*</span>
          </Label>
          <Input
            id="name"
            placeholder="e.g., Production Devices"
            {...register('name', {
              required: 'Group name is required',
              minLength: {
                value: 3,
                message: 'Group name must be at least 3 characters',
              },
              maxLength: {
                value: 100,
                message: 'Group name must be at most 100 characters',
              },
            })}
            disabled={isLoading}
          />
          {errors.name && (
            <p className="text-sm text-destructive">{errors.name.message}</p>
          )}
        </div>

        {/* Group Description */}
        <div className="space-y-2">
          <Label htmlFor="description">Description</Label>
          <Textarea
            id="description"
            placeholder="Describe the purpose of this group..."
            rows={3}
            {...register('description', {
              maxLength: {
                value: 500,
                message: 'Description must be at most 500 characters',
              },
            })}
            disabled={isLoading}
          />
          {errors.description && (
            <p className="text-sm text-destructive">{errors.description.message}</p>
          )}
        </div>

        {/* Group Type */}
        <div className="space-y-3">
          <Label>
            Group Type <span className="text-destructive">*</span>
          </Label>
          <RadioGroup
            value={groupType}
            onValueChange={(value: string) => setValue('type', value as GroupType, { shouldDirty: true })}
            disabled={isLoading || isEditMode}
            className="space-y-3"
          >
            {/* Static Group Option */}
            <Card
              className={cn(
                'cursor-pointer transition-colors',
                groupType === GroupType.Static && 'border-primary',
                isEditMode && 'cursor-not-allowed opacity-60'
              )}
              onClick={() => !isEditMode && !isLoading && setValue('type', GroupType.Static, { shouldDirty: true })}
            >
              <CardHeader className="pb-3">
                <div className="flex items-start gap-3">
                  <RadioGroupItem value={GroupType.Static} id="static" className="mt-1" />
                  <div className="flex-1">
                    <Label htmlFor="static" className="cursor-pointer font-semibold">
                      Static Group
                    </Label>
                    <CardDescription className="mt-1">
                      Manually add and remove devices. Full control over membership.
                    </CardDescription>
                  </div>
                </div>
              </CardHeader>
            </Card>

            {/* Dynamic Group Option */}
            <Card
              className={cn(
                'cursor-pointer transition-colors',
                groupType === GroupType.Dynamic && 'border-primary',
                isEditMode && 'cursor-not-allowed opacity-60'
              )}
              onClick={() => !isEditMode && !isLoading && setValue('type', GroupType.Dynamic, { shouldDirty: true })}
            >
              <CardHeader className="pb-3">
                <div className="flex items-start gap-3">
                  <RadioGroupItem value={GroupType.Dynamic} id="dynamic" className="mt-1" />
                  <div className="flex-1">
                    <Label htmlFor="dynamic" className="cursor-pointer font-semibold">
                      Dynamic Group
                    </Label>
                    <CardDescription className="mt-1">
                      Automatically include devices based on tag query. Membership updates automatically.
                    </CardDescription>
                  </div>
                </div>
              </CardHeader>
            </Card>
          </RadioGroup>

          {isEditMode && (
            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                Group type cannot be changed after creation. Create a new group to use a different type.
              </AlertDescription>
            </Alert>
          )}
        </div>

        {/* Tag Query (Dynamic Groups Only) */}
        {groupType === GroupType.Dynamic && (
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <Label htmlFor="tagQuery">
                Tag Query <span className="text-destructive">*</span>
              </Label>
              <Popover>
                <PopoverTrigger asChild>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="h-5 w-5 p-0"
                  >
                    <HelpCircle className="h-4 w-4" />
                  </Button>
                </PopoverTrigger>
                <PopoverContent className="w-[600px]" align="start">
                  <div className="space-y-3">
                    <div>
                      <h4 className="mb-2 font-semibold">Query Syntax</h4>
                      <div className="space-y-2 text-sm">
                        <div className="flex items-start gap-2">
                          <Badge variant="outline" className="shrink-0">
                            AND
                          </Badge>
                          <span className="text-muted-foreground">
                            Match devices with both conditions
                          </span>
                        </div>
                        <div className="flex items-start gap-2">
                          <Badge variant="outline" className="shrink-0">
                            OR
                          </Badge>
                          <span className="text-muted-foreground">
                            Match devices with either condition
                          </span>
                        </div>
                        <div className="flex items-start gap-2">
                          <Badge variant="outline" className="shrink-0">
                            NOT
                          </Badge>
                          <span className="text-muted-foreground">
                            Exclude devices matching condition
                          </span>
                        </div>
                        <div className="flex items-start gap-2">
                          <Badge variant="outline" className="shrink-0">
                            *
                          </Badge>
                          <span className="text-muted-foreground">
                            Wildcard matching (e.g., warehouse-*)
                          </span>
                        </div>
                        <div className="flex items-start gap-2">
                          <Badge variant="outline" className="shrink-0">
                            ( )
                          </Badge>
                          <span className="text-muted-foreground">
                            Grouping for complex queries
                          </span>
                        </div>
                      </div>
                    </div>

                    <div>
                      <h4 className="mb-2 font-semibold">Example Queries</h4>
                      <div className="space-y-2">
                        {exampleQueries.map((example, index) => (
                          <button
                            key={index}
                            type="button"
                            onClick={() => {
                              setValue('tagQuery', example.query, { shouldDirty: true })
                            }}
                            className="block w-full rounded-md border p-2 text-left transition-colors hover:bg-accent"
                          >
                            <code className="block text-xs font-mono text-primary">
                              {example.query}
                            </code>
                            <span className="text-xs text-muted-foreground">
                              {example.description}
                            </span>
                          </button>
                        ))}
                      </div>
                    </div>
                  </div>
                </PopoverContent>
              </Popover>
            </div>

            <Input
              id="tagQuery"
              placeholder="e.g., environment=production AND location=warehouse-*"
              {...register('tagQuery', {
                required:
                  groupType === GroupType.Dynamic ? 'Tag query is required for dynamic groups' : false,
                minLength: {
                  value: 3,
                  message: 'Tag query must be at least 3 characters',
                },
              })}
              disabled={isLoading}
            />
            {errors.tagQuery && (
              <p className="text-sm text-destructive">{errors.tagQuery.message}</p>
            )}

            {tagQuery && (
              <div className="rounded-md bg-muted p-3">
                <p className="mb-1 text-xs font-medium text-muted-foreground">
                  Current query:
                </p>
                <code className="text-sm font-mono">{tagQuery}</code>
              </div>
            )}

            <Alert>
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                Devices matching this query will be automatically added to the group. Membership updates every 5 minutes.
              </AlertDescription>
            </Alert>
          </div>
        )}

        {/* Error Display */}
        {error && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {/* Form Actions */}
        <div className="flex justify-end gap-2">
          {onCancel && (
            <Button
              type="button"
              variant="outline"
              onClick={onCancel}
              disabled={isLoading}
            >
              Cancel
            </Button>
          )}
          <Button type="submit" disabled={isLoading || !isDirty}>
            {isLoading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            {isEditMode ? 'Update Group' : 'Create Group'}
          </Button>
        </div>
      </div>
    </form>
  )
}
