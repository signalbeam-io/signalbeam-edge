/**
 * CreateBundle Dialog Component
 */

import { useState } from 'react'
import { useForm, useFieldArray } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Plus, Trash2, Package } from 'lucide-react'
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
import { Textarea } from '@/components/ui/textarea'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useCreateBundle } from '@/hooks/api/use-bundles'
import { createBundleSchema, CreateBundleFormData } from '../validation/bundle-schemas'
import { useToast } from '@/hooks/use-toast'

interface CreateBundleDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function CreateBundleDialog({ open, onOpenChange }: CreateBundleDialogProps) {
  const { toast } = useToast()
  const createBundle = useCreateBundle()
  const [isSubmitting, setIsSubmitting] = useState(false)

  const form = useForm<CreateBundleFormData>({
    resolver: zodResolver(createBundleSchema),
    defaultValues: {
      name: '',
      description: '',
      version: '1.0.0',
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

  const onSubmit = async (data: CreateBundleFormData) => {
    setIsSubmitting(true)
    try {
      const payload = {
        name: data.name,
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
        ...(data.description && { description: data.description }),
      }
      await createBundle.mutateAsync(payload)
      toast({
        title: 'Bundle created',
        description: `Bundle "${data.name}" has been created successfully.`,
      })
      form.reset()
      onOpenChange(false)
    } catch (error) {
      toast({
        title: 'Failed to create bundle',
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

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Create New Bundle</DialogTitle>
          <DialogDescription>
            Create a new application bundle to deploy containers to your edge devices.
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
            {/* Bundle Details */}
            <div className="space-y-4">
              <FormField
                control={form.control}
                name="name"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Bundle Name</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g., warehouse-monitor" {...field} />
                    </FormControl>
                    <FormDescription>A unique name for this bundle</FormDescription>
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
                        placeholder="Describe what this bundle does..."
                        className="resize-none"
                        {...field}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="version"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Initial Version</FormLabel>
                    <FormControl>
                      <Input placeholder="1.0.0" {...field} />
                    </FormControl>
                    <FormDescription>Use semantic versioning (e.g., 1.0.0)</FormDescription>
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
                    Define the containers to run on devices
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
                    <Package className="mr-2 h-4 w-4" />
                    Create Bundle
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
