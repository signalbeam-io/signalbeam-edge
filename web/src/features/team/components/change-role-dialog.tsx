/**
 * Dialog for changing a team member's role
 */

import { useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { TeamMember } from '@/api/types/team'
import type { UserRole } from '@/stores/auth-store'

interface ChangeRoleDialogProps {
  member: TeamMember | null
  onOpenChange: (open: boolean) => void
  onChangeRole: (userId: string, role: UserRole) => void
  isPending: boolean
}

export function ChangeRoleDialog({ member, onOpenChange, onChangeRole, isPending }: ChangeRoleDialogProps) {
  const [role, setRole] = useState<UserRole>(member?.role ?? 'DeviceOwner')

  const handleSubmit = () => {
    if (member) {
      onChangeRole(member.userId, role)
    }
  }

  return (
    <Dialog open={!!member} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle>Change Role</DialogTitle>
          <DialogDescription>
            Change the role for {member?.name ?? member?.email}.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label>New Role</Label>
            <Select value={role} onValueChange={(v) => setRole(v as UserRole)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="DeviceOwner">Device Owner</SelectItem>
                <SelectItem value="Admin">Admin</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={isPending || role === member?.role}>
            {isPending ? 'Saving...' : 'Save'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
