/**
 * CreateVersion Dialog Component
 */

import { useState, useEffect } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Plus, Trash2, GitBranch } from 'lucide-react'
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
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useCreateBundleVersion } from '@/hooks/api/use-bundles'
import { createBundleVersionSchema, CreateBundleVersionFormData } from '../validation/bundle-schemas'
import { useToast } from '@/hooks/use-toast'
import { AppBundle } from '@/api/types'

interface CreateVersionDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  bundle: AppBundle | null
}

export function CreateVersionDialog({ open, onOpenChange, bundle }: CreateVersionDialogProps) {
  const { toast } = useToast()
  const createVersion = useCreateBundleVersion()
  const [isSubmitting, setIsSubmitting] = useState(false)

  const form = useForm<CreateBundleVersionFormData>({
    resolver: zodResolver(createBundleVersionSchema),
    defaultValues: {
      version: '',
      containers: [
        {
          name: '',
          image: '',
          tag: 'latest',
          environment: {},
          ports: [],
          volumes: [],
        },
      ],
    },
  })

  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: 'containers',
  })

  // Reset form when bundle changes
  useEffect(() => {
    if (bundle && open) {
      // Pre-fill with current version's containers
      const activeVersion = bundle.versions?.find((v) => v.isActive)
      if (activeVersion) {
        form.reset({
          version: '', // Let user specify new version
          containers: activeVersion.containers.map((c) => ({
            name: c.name,
            image: c.image,
            tag: c.tag,
            ...(c.environment && { environment: c.environment }),
            ...(c.ports && c.ports.length > 0 && { ports: c.ports }),
            ...(c.volumes && c.volumes.length > 0 && { volumes: c.volumes }),
          })),
        })
      }
    }
  }, [bundle, open, form])

  const onSubmit = async (data: CreateBundleVersionFormData) => {
    if (!bundle) return

    setIsSubmitting(true)
    try {
      const payload = {
        version: data.version,
        containers: data.containers.map((c) => ({
          name: c.name,
          image: c.image,
          tag: c.tag,
          ...(c.environment && Object.keys(c.environment).length > 0 && { environment: c.environment }),
          ...(c.ports && c.ports.length > 0 && { ports: c.ports }),
          ...(c.volumes && c.volumes.length > 0 && {
            volumes: c.volumes.map((v) => ({
              hostPath: v.hostPath,
              containerPath: v.containerPath,
              ...(v.readOnly !== undefined && { readOnly: v.readOnly }),
            })),
          }),
        })),
      }
      await createVersion.mutateAsync({
        bundleId: bundle.id,
        data: payload,
      })
      toast({
        title: 'Version created',
        description: `Version ${data.version} has been created for "${bundle.name}".`,
      })
      form.reset()
      onOpenChange(false)
    } catch (error) {
      toast({
        title: 'Failed to create version',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleAddContainer = () => {
    append({
      name: '',
      image: '',
      tag: 'latest',
      environment: {},
      ports: [],
      volumes: [],
    })
  }

  const handleClose = () => {
    form.reset()
    onOpenChange(false)
  }

  if (!bundle) return null

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Create New Version</DialogTitle>
          <DialogDescription>
            Create a new version for bundle "{bundle.name}"
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
            {/* Version Details */}
            <div className="space-y-4">
              <FormField
                control={form.control}
                name="version"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Version Number</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., 1.1.0" {...field} />
                    </FormControl>
                    <FormDescription>
                      Use semantic versioning (current: v{bundle.currentVersion})
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            {/* Container Definitions */}
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-lg font-semibold">Containers</h3>
                  <p className="text-sm text-muted-foreground">
                    Define the containers for this version
                  </p>
                </div>
                <Button type="button" variant="outline" size="sm" onClick={handleAddContainer}>
                  <Plus className="mr-2 h-4 w-4" />
                  Add Container
                </Button>
              </div>

              {fields.map((field, index) => (
                <Card key={field.id}>
                  <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
                    <CardTitle className="text-sm font-medium">
                      Container {index + 1}
                    </CardTitle>
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
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <FormField
                      control={form.control}
                      name={`containers.${index}.name`}
                      render={({ field }) => (
                        <FormItem>
                          <FormLabel>Container Name</FormLabel>
                          <FormControl>
                            <Input placeholder="e.g., temp-sensor" {...field} />
                          </FormControl>
                          <FormDescription>
                            Lowercase alphanumeric with hyphens
                          </FormDescription>
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    <div className="grid grid-cols-2 gap-4">
                      <FormField
                        control={form.control}
                        name={`containers.${index}.image`}
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Image</FormLabel>
                            <FormControl>
                              <Input placeholder="e.g., ghcr.io/org/image" {...field} />
                            </FormControl>
                            <FormMessage />
                          </FormItem>
                        )}
                      />

                      <FormField
                        control={form.control}
                        name={`containers.${index}.tag`}
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Tag</FormLabel>
                            <FormControl>
                              <Input placeholder="latest" {...field} />
                            </FormControl>
                            <FormMessage />
                          </FormItem>
                        )}
                      />
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={handleClose}>
                Cancel
              </Button>
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? (
                  <>Creating...</>
                ) : (
                  <>
                    <GitBranch className="mr-2 h-4 w-4" />
                    Create Version
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
