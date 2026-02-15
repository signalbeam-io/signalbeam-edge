/**
 * Confirmation dialog for removing a team member
 */

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
import type { TeamMember } from '@/api/types/team'

interface RemoveUserDialogProps {
  member: TeamMember | null
  onOpenChange: (open: boolean) => void
  onConfirm: () => void
}

export function RemoveUserDialog({ member, onOpenChange, onConfirm }: RemoveUserDialogProps) {
  return (
    <AlertDialog open={!!member} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Remove Team Member?</AlertDialogTitle>
          <AlertDialogDescription>
            This will remove <strong>{member?.name ?? member?.email}</strong> from the team.
            They will lose access to all resources. This action cannot be undone.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction onClick={onConfirm}>Remove Member</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
