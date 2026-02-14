import { useEffect, useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useDeleteAccount } from '@/hooks/api/use-users'
import { useToast } from '@/hooks/use-toast'
import { getErrorMessage } from '@/api/client'

interface DeleteAccountDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function DeleteAccountDialog({ open, onOpenChange }: DeleteAccountDialogProps) {
  const [confirmation, setConfirmation] = useState('')
  const deleteAccount = useDeleteAccount()

  useEffect(() => {
    if (open) setConfirmation('')
  }, [open])
  const { toast } = useToast()

  const isConfirmed = confirmation === 'DELETE'

  const handleDelete = () => {
    if (!isConfirmed) return

    deleteAccount.mutate(undefined, {
      onError: (error) => {
        toast({
          title: 'Failed to delete account',
          description: getErrorMessage(error),
          variant: 'destructive',
        })
      },
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Delete Account</DialogTitle>
          <DialogDescription>
            This action is permanent and cannot be undone. All your data will be deleted.
          </DialogDescription>
        </DialogHeader>
        <div className="py-4">
          <Label htmlFor="confirmation">
            Type <span className="font-mono font-bold">DELETE</span> to confirm
          </Label>
          <Input
            id="confirmation"
            value={confirmation}
            onChange={(e) => setConfirmation(e.target.value)}
            placeholder="DELETE"
            className="mt-1.5"
          />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            variant="destructive"
            onClick={handleDelete}
            disabled={!isConfirmed || deleteAccount.isPending}
          >
            {deleteAccount.isPending ? 'Deleting...' : 'Delete Account'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
