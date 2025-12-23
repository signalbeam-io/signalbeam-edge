/**
 * AssignBundle Dialog Component
 */

import { useState, useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from 'react-router-dom'
import { Users, ChevronDown, ChevronRight } from 'lucide-react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { useCreateRollout } from '@/hooks/api/use-rollouts'
import { useGroups } from '@/hooks/api/use-groups'
import { useDevices } from '@/hooks/api/use-devices'
import { assignBundleSchema, AssignBundleFormData } from '../validation/bundle-schemas'
import { useToast } from '@/hooks/use-toast'
import { AppBundle } from '@/api/types'
import { Skeleton } from '@/components/ui/skeleton'

interface AssignBundleDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  bundle: AppBundle | null
}

export function AssignBundleDialog({ open, onOpenChange, bundle }: AssignBundleDialogProps) {
  const { toast } = useToast()
  const navigate = useNavigate()
  const createRollout = useCreateRollout()
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [showGroups, setShowGroups] = useState(true)
  const [showDevices, setShowDevices] = useState(true)

  // Fetch groups and devices
  const { data: groupsData, isLoading: groupsLoading } = useGroups({ pageSize: 100 })
  const { data: devicesData, isLoading: devicesLoading } = useDevices({ pageSize: 100 })

  const form = useForm<AssignBundleFormData>({
    resolver: zodResolver(assignBundleSchema),
    defaultValues: {
      groupIds: [],
      deviceIds: [],
    },
  })

  // Reset form when dialog opens
  useEffect(() => {
    if (open) {
      form.reset({
        groupIds: [],
        deviceIds: [],
      })
    }
  }, [open, form])

  const onSubmit = async (data: AssignBundleFormData) => {
    if (!bundle) return

    setIsSubmitting(true)
    try {
      let rolloutId: string | null = null

      // Create rollout for groups if any selected
      if (data.groupIds && data.groupIds.length > 0) {
        const rollout = await createRollout.mutateAsync({
          bundleId: bundle.id,
          version: bundle.currentVersion,
          targetType: 'group',
          targetIds: data.groupIds,
        })
        rolloutId = rollout.id
      }

      // Create rollout for devices if any selected
      if (data.deviceIds && data.deviceIds.length > 0) {
        const rollout = await createRollout.mutateAsync({
          bundleId: bundle.id,
          version: bundle.currentVersion,
          targetType: 'device',
          targetIds: data.deviceIds,
        })
        rolloutId = rollout.id
      }

      const totalTargets = (data.groupIds?.length || 0) + (data.deviceIds?.length || 0)

      form.reset()
      onOpenChange(false)

      // Navigate to rollout status page if a rollout was created
      if (rolloutId) {
        toast({
          title: 'Rollout created',
          description: `Bundle "${bundle.name}" v${bundle.currentVersion} is being deployed to ${totalTargets} target(s).`,
        })
        navigate(`/rollouts/${rolloutId}`)
      } else {
        toast({
          title: 'Bundle assigned',
          description: `Bundle "${bundle.name}" v${bundle.currentVersion} has been assigned to ${totalTargets} target(s).`,
        })
      }
    } catch (error) {
      toast({
        title: 'Failed to assign bundle',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleClose = () => {
    form.reset()
    onOpenChange(false)
  }

  if (!bundle) return null

  const groups = groupsData?.data || []
  const devices = devicesData?.data || []
  const isLoading = groupsLoading || devicesLoading

  const selectedGroupCount = form.watch('groupIds')?.length || 0
  const selectedDeviceCount = form.watch('deviceIds')?.length || 0
  const totalSelected = selectedGroupCount + selectedDeviceCount

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Assign Bundle to Devices/Groups</DialogTitle>
          <DialogDescription>
            Assign "{bundle.name}" v{bundle.currentVersion} to device groups or individual
            devices.
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
            {/* Groups Section */}
            <div className="space-y-3">
              <button
                type="button"
                className="flex w-full items-center justify-between rounded-md border p-3 hover:bg-muted/50"
                onClick={() => setShowGroups(!showGroups)}
              >
                <div className="flex items-center gap-2">
                  {showGroups ? (
                    <ChevronDown className="h-4 w-4" />
                  ) : (
                    <ChevronRight className="h-4 w-4" />
                  )}
                  <h3 className="text-sm font-semibold">Device Groups</h3>
                  {selectedGroupCount > 0 && (
                    <span className="rounded-full bg-primary px-2 py-0.5 text-xs text-primary-foreground">
                      {selectedGroupCount} selected
                    </span>
                  )}
                </div>
              </button>

              {showGroups && (
                <FormField
                  control={form.control}
                  name="groupIds"
                  render={() => (
                    <FormItem>
                      <div className="space-y-2">
                        {isLoading ? (
                          <div className="space-y-2">
                            <Skeleton className="h-8 w-full" />
                            <Skeleton className="h-8 w-full" />
                            <Skeleton className="h-8 w-full" />
                          </div>
                        ) : groups.length === 0 ? (
                          <p className="text-sm text-muted-foreground py-4 text-center">
                            No groups available
                          </p>
                        ) : (
                          groups.map((group) => (
                            <FormField
                              key={group.id}
                              control={form.control}
                              name="groupIds"
                              render={({ field }) => (
                                <FormItem className="flex items-center space-x-3 space-y-0 rounded-md border p-3 hover:bg-muted/50">
                                  <FormControl>
                                    <Checkbox
                                      checked={field.value?.includes(group.id) || false}
                                      onCheckedChange={(checked) => {
                                        const value = field.value || []
                                        if (checked) {
                                          field.onChange([...value, group.id])
                                        } else {
                                          field.onChange(value.filter((id) => id !== group.id))
                                        }
                                      }}
                                    />
                                  </FormControl>
                                  <div className="flex-1">
                                    <FormLabel className="font-medium cursor-pointer">
                                      {group.name}
                                    </FormLabel>
                                    {group.description && (
                                      <p className="text-xs text-muted-foreground">
                                        {group.description}
                                      </p>
                                    )}
                                  </div>
                                </FormItem>
                              )}
                            />
                          ))
                        )}
                      </div>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}
            </div>

            {/* Devices Section */}
            <div className="space-y-3">
              <button
                type="button"
                className="flex w-full items-center justify-between rounded-md border p-3 hover:bg-muted/50"
                onClick={() => setShowDevices(!showDevices)}
              >
                <div className="flex items-center gap-2">
                  {showDevices ? (
                    <ChevronDown className="h-4 w-4" />
                  ) : (
                    <ChevronRight className="h-4 w-4" />
                  )}
                  <h3 className="text-sm font-semibold">Individual Devices</h3>
                  {selectedDeviceCount > 0 && (
                    <span className="rounded-full bg-primary px-2 py-0.5 text-xs text-primary-foreground">
                      {selectedDeviceCount} selected
                    </span>
                  )}
                </div>
              </button>

              {showDevices && (
                <FormField
                  control={form.control}
                  name="deviceIds"
                  render={() => (
                    <FormItem>
                      <div className="max-h-60 space-y-2 overflow-y-auto">
                        {isLoading ? (
                          <div className="space-y-2">
                            <Skeleton className="h-8 w-full" />
                            <Skeleton className="h-8 w-full" />
                            <Skeleton className="h-8 w-full" />
                          </div>
                        ) : devices.length === 0 ? (
                          <p className="text-sm text-muted-foreground py-4 text-center">
                            No devices available
                          </p>
                        ) : (
                          devices.map((device) => (
                            <FormField
                              key={device.id}
                              control={form.control}
                              name="deviceIds"
                              render={({ field }) => (
                                <FormItem className="flex items-center space-x-3 space-y-0 rounded-md border p-3 hover:bg-muted/50">
                                  <FormControl>
                                    <Checkbox
                                      checked={field.value?.includes(device.id) || false}
                                      onCheckedChange={(checked) => {
                                        const value = field.value || []
                                        if (checked) {
                                          field.onChange([...value, device.id])
                                        } else {
                                          field.onChange(value.filter((id) => id !== device.id))
                                        }
                                      }}
                                    />
                                  </FormControl>
                                  <div className="flex-1">
                                    <FormLabel className="font-medium cursor-pointer">
                                      {device.name}
                                    </FormLabel>
                                    <p className="text-xs text-muted-foreground">{device.id}</p>
                                  </div>
                                </FormItem>
                              )}
                            />
                          ))
                        )}
                      </div>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}
            </div>

            <p className="text-sm text-muted-foreground">
              {totalSelected === 0
                ? 'Select at least one group or device to assign this bundle'
                : `${totalSelected} target(s) selected`}
            </p>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={handleClose}>
                Cancel
              </Button>
              <Button type="submit" disabled={isSubmitting || totalSelected === 0}>
                {isSubmitting ? (
                  <>Assigning...</>
                ) : (
                  <>
                    <Users className="mr-2 h-4 w-4" />
                    Assign Bundle
                  </>
                )}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  )
}
