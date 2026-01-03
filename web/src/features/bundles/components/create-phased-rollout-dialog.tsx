/**
 * CreatePhasedRollout Dialog Component
 */

import { useState, useEffect } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from 'react-router-dom'
import { Plus, Trash2, Rocket } from 'lucide-react'
import { z } from 'zod'
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
  FormDescription,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Button } from '@/components/ui/button'
import { useCreatePhasedRollout } from '@/hooks/api/use-phased-rollouts'
import { useToast } from '@/hooks/use-toast'
import type { AppBundle } from '@/api/types'

const phaseConfigSchema = z.object({
  phaseNumber: z.number().int().positive(),
  name: z.string().min(1, 'Phase name is required'),
  targetPercentage: z.number().min(0).max(100),
  targetDeviceCount: z.number().int().positive().nullable(),
  minHealthyDurationMinutes: z.number().int().positive().nullable(),
})

const phasedRolloutSchema = z.object({
  name: z.string().min(1, 'Rollout name is required'),
  description: z.string().optional(),
  failureThreshold: z.number().min(0).max(1).default(0.05),
  phases: z.array(phaseConfigSchema).min(1, 'At least one phase is required'),
})

type PhasedRolloutFormData = z.infer<typeof phasedRolloutSchema>

interface CreatePhasedRolloutDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  bundle: AppBundle | null
}

export function CreatePhasedRolloutDialog({
  open,
  onOpenChange,
  bundle,
}: CreatePhasedRolloutDialogProps) {
  const { toast } = useToast()
  const navigate = useNavigate()
  const createPhasedRollout = useCreatePhasedRollout()
  const [isSubmitting, setIsSubmitting] = useState(false)

  const form = useForm<PhasedRolloutFormData>({
    resolver: zodResolver(phasedRolloutSchema),
    defaultValues: {
      name: '',
      description: '',
      failureThreshold: 0.05,
      phases: [
        {
          phaseNumber: 1,
          name: 'Canary',
          targetPercentage: 10,
          targetDeviceCount: null,
          minHealthyDurationMinutes: 5,
        },
      ],
    },
  })

  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: 'phases',
  })

  // Reset form when dialog opens
  useEffect(() => {
    if (open && bundle) {
      form.reset({
        name: `${bundle.name} ${bundle.currentVersion} Rollout`,
        description: `Phased rollout for ${bundle.name} version ${bundle.currentVersion}`,
        failureThreshold: 0.05,
        phases: [
          {
            phaseNumber: 1,
            name: 'Canary',
            targetPercentage: 10,
            targetDeviceCount: null,
            minHealthyDurationMinutes: 5,
          },
        ],
      })
    }
  }, [open, bundle, form])

  const onSubmit = async (data: PhasedRolloutFormData) => {
    if (!bundle) return

    setIsSubmitting(true)
    try {
      const rollout = await createPhasedRollout.mutateAsync({
        bundleId: bundle.id,
        targetVersion: bundle.currentVersion,
        name: data.name,
        ...(data.description && { description: data.description }),
        failureThreshold: data.failureThreshold,
        phases: data.phases,
      })

      form.reset()
      onOpenChange(false)

      toast({
        title: 'Phased rollout created',
        description: `Rollout "${data.name}" created with ${data.phases.length} phase(s).`,
      })

      // Navigate to phased rollout detail page
      navigate(`/phased-rollouts/${rollout.id}`)
    } catch (error) {
      toast({
        title: 'Failed to create phased rollout',
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

  const addPhase = () => {
    const nextPhaseNumber = fields.length + 1
    append({
      phaseNumber: nextPhaseNumber,
      name: `Phase ${nextPhaseNumber}`,
      targetPercentage: nextPhaseNumber === fields.length + 1 ? 100 : 50,
      targetDeviceCount: null,
      minHealthyDurationMinutes: 10,
    })
  }

  if (!bundle) return null

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Create Phased Rollout</DialogTitle>
          <DialogDescription>
            Configure a multi-phase rollout for "{bundle.name}" v{bundle.currentVersion}
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
            {/* Basic Info */}
            <div className="space-y-4">
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Rollout Name</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., Production Rollout Q1 2025" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="description"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Description (Optional)</FormLabel>
                    <FormControl>
                      <Textarea
                        placeholder="Add notes about this rollout..."
                        rows={2}
                        {...field}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="failureThreshold"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Failure Threshold</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        step="0.01"
                        min="0"
                        max="1"
                        placeholder="0.05"
                        {...field}
                        onChange={(e) => field.onChange(parseFloat(e.target.value))}
                      />
                    </FormControl>
                    <FormDescription>
                      Rollout will auto-rollback if failure rate exceeds this threshold (e.g., 0.05
                      = 5%)
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            {/* Phases Configuration */}
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-lg font-semibold">Rollout Phases</h3>
                <Button type="button" variant="outline" size="sm" onClick={addPhase}>
                  <Plus className="mr-2 h-4 w-4" />
                  Add Phase
                </Button>
              </div>

              <div className="space-y-4">
                {fields.map((field, index) => (
                  <div key={field.id} className="rounded-lg border p-4 space-y-4">
                    <div className="flex items-center justify-between">
                      <h4 className="font-medium">Phase {index + 1}</h4>
                      {fields.length > 1 && (
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => remove(index)}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      )}
                    </div>

                    <div className="grid grid-cols-2 gap-4">
                      <FormField
                        control={form.control}
                        name={`phases.${index}.name`}
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Phase Name</FormLabel>
                            <FormControl>
                              <Input placeholder="e.g., Canary" {...field} />
                            </FormControl>
                            <FormMessage />
                          </FormItem>
                        )}
                      />

                      <FormField
                        control={form.control}
                        name={`phases.${index}.targetPercentage`}
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Target Percentage (%)</FormLabel>
                            <FormControl>
                              <Input
                                type="number"
                                min="0"
                                max="100"
                                placeholder="10"
                                {...field}
                                onChange={(e) => field.onChange(parseFloat(e.target.value))}
                              />
                            </FormControl>
                            <FormMessage />
                          </FormItem>
                        )}
                      />

                      <FormField
                        control={form.control}
                        name={`phases.${index}.targetDeviceCount`}
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Target Device Count (Optional)</FormLabel>
                            <FormControl>
                              <Input
                                type="number"
                                min="1"
                                placeholder="Leave empty for percentage-based"
                                {...field}
                                value={field.value ?? ''}
                                onChange={(e) =>
                                  field.onChange(
                                    e.target.value ? parseInt(e.target.value) : null
                                  )
                                }
                              />
                            </FormControl>
                            <FormMessage />
                          </FormItem>
                        )}
                      />

                      <FormField
                        control={form.control}
                        name={`phases.${index}.minHealthyDurationMinutes`}
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Min Healthy Duration (minutes)</FormLabel>
                            <FormControl>
                              <Input
                                type="number"
                                min="1"
                                placeholder="5"
                                {...field}
                                value={field.value ?? ''}
                                onChange={(e) =>
                                  field.onChange(
                                    e.target.value ? parseInt(e.target.value) : null
                                  )
                                }
                              />
                            </FormControl>
                            <FormDescription>
                              Phase must be healthy for this duration before advancing
                            </FormDescription>
                            <FormMessage />
                          </FormItem>
                        )}
                      />
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={handleClose}>
                Cancel
              </Button>
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? (
                  'Creating...'
                ) : (
                  <>
                    <Rocket className="mr-2 h-4 w-4" />
                    Create Rollout
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
