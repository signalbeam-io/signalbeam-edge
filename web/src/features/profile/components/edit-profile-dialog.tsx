import React, { useEffect, useState } from 'react'
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
import { useUpdateProfile } from '@/hooks/api/use-users'
import { useToast } from '@/hooks/use-toast'
import { getErrorMessage } from '@/api/client'

interface EditProfileDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  currentName: string
}

export function EditProfileDialog({ open, onOpenChange, currentName }: EditProfileDialogProps) {
  const [name, setName] = useState(currentName)
  const updateProfile = useUpdateProfile()

  useEffect(() => {
    if (open) setName(currentName)
  }, [open, currentName])
  const { toast } = useToast()

  const isValid = name.trim().length >= 1 && name.trim().length <= 200

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!isValid) return

    updateProfile.mutate(name.trim(), {
      onSuccess: () => {
        toast({ title: 'Profile updated', description: 'Your name has been updated successfully.' })
        onOpenChange(false)
      },
      onError: (error) => {
        toast({
          title: 'Failed to update profile',
          description: getErrorMessage(error),
          variant: 'destructive',
        })
      },
    })
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={handleSubmit}>
          <DialogHeader>
            <DialogTitle>Edit Profile</DialogTitle>
            <DialogDescription>Update your display name.</DialogDescription>
          </DialogHeader>
          <div className="py-4">
            <Label htmlFor="name">Name</Label>
            <Input
              id="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              maxLength={200}
              className="mt-1.5"
            />
            {name.trim().length === 0 && (
              <p className="mt-1 text-sm text-destructive">Name is required.</p>
            )}
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={!isValid || updateProfile.isPending}>
              {updateProfile.isPending ? 'Saving...' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
