/**
 * Bundles Page
 */

import { useState } from 'react'
import { BundlesList } from '../components/bundles-list'
import { CreateBundleDialog } from '../components/create-bundle-dialog'
import { CreateVersionDialog } from '../components/create-version-dialog'
import { AssignBundleDialog } from '../components/assign-bundle-dialog'
import { useDeleteBundle } from '@/hooks/api/use-bundles'
import { useToast } from '@/hooks/use-toast'
import { AppBundle } from '@/api/types'
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

export function BundlesPage() {
  const { toast } = useToast()
  const deleteBundle = useDeleteBundle()

  // Dialog states
  const [createBundleOpen, setCreateBundleOpen] = useState(false)
  const [createVersionOpen, setCreateVersionOpen] = useState(false)
  const [assignBundleOpen, setAssignBundleOpen] = useState(false)
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)

  // Selected bundle for dialogs
  const [selectedBundle, setSelectedBundle] = useState<AppBundle | null>(null)

  const handleCreateBundle = () => {
    setCreateBundleOpen(true)
  }

  const handleCreateVersion = (bundle: AppBundle) => {
    setSelectedBundle(bundle)
    setCreateVersionOpen(true)
  }

  const handleAssignBundle = (bundle: AppBundle) => {
    setSelectedBundle(bundle)
    setAssignBundleOpen(true)
  }

  const handleEditBundle = (bundle: AppBundle) => {
    // TODO: Implement edit dialog
    toast({
      title: 'Edit bundle',
      description: `Editing "${bundle.name}" - This feature is coming soon!`,
    })
  }

  const handleDeleteBundle = (bundle: AppBundle) => {
    setSelectedBundle(bundle)
    setDeleteDialogOpen(true)
  }

  const confirmDelete = async () => {
    if (!selectedBundle) return

    try {
      await deleteBundle.mutateAsync(selectedBundle.id)
      toast({
        title: 'Bundle deleted',
        description: `Bundle "${selectedBundle.name}" has been deleted successfully.`,
      })
      setDeleteDialogOpen(false)
      setSelectedBundle(null)
    } catch (error) {
      toast({
        title: 'Failed to delete bundle',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    }
  }

  return (
    <div className="container mx-auto py-6">
      <BundlesList
        onCreateBundle={handleCreateBundle}
        onCreateVersion={handleCreateVersion}
        onAssignBundle={handleAssignBundle}
        onEditBundle={handleEditBundle}
        onDeleteBundle={handleDeleteBundle}
      />

      {/* Create Bundle Dialog */}
      <CreateBundleDialog open={createBundleOpen} onOpenChange={setCreateBundleOpen} />

      {/* Create Version Dialog */}
      <CreateVersionDialog
        open={createVersionOpen}
        onOpenChange={setCreateVersionOpen}
        bundle={selectedBundle}
      />

      {/* Assign Bundle Dialog */}
      <AssignBundleDialog
        open={assignBundleOpen}
        onOpenChange={setAssignBundleOpen}
        bundle={selectedBundle}
      />

      {/* Delete Confirmation Dialog */}
      <AlertDialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Are you sure?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete the bundle "{selectedBundle?.name}" and all its versions.
              This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={confirmDelete} className="bg-destructive hover:bg-destructive/90">
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
