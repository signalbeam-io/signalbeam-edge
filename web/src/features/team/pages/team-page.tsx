/**
 * Team Management Page
 */

import { useState } from 'react'
import { Plus, UserCog, Trash2, Mail, Users, AlertCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { useToast } from '@/hooks/use-toast'
import { useAuthStore } from '@/stores/auth-store'
import {
  useTeamMembers,
  usePendingInvitations,
  useInviteUser,
  useChangeRole,
  useRemoveUser,
} from '@/hooks/api/use-team'
import { InviteUserDialog } from '../components/invite-user-dialog'
import { ChangeRoleDialog } from '../components/change-role-dialog'
import { RemoveUserDialog } from '../components/remove-user-dialog'
import { formatDistanceToNow } from 'date-fns'
import type { TeamMember } from '@/api/types/team'
import type { UserRole } from '@/stores/auth-store'

export function TeamPage() {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [isInviteOpen, setIsInviteOpen] = useState(false)
  const [changeRoleMember, setChangeRoleMember] = useState<TeamMember | null>(null)
  const [removeMember, setRemoveMember] = useState<TeamMember | null>(null)
  const { toast } = useToast()
  const currentUser = useAuthStore((s) => s.user)

  const { data: membersData, isLoading: membersLoading } = useTeamMembers({
    page,
    pageSize: 20,
    search: search || undefined,
  })
  const { data: invitations, isLoading: invitationsLoading } = usePendingInvitations()
  const inviteMutation = useInviteUser()
  const changeRoleMutation = useChangeRole()
  const removeMutation = useRemoveUser()

  const handleInvite = async (email: string, role: UserRole) => {
    try {
      await inviteMutation.mutateAsync({ email, role })
      setIsInviteOpen(false)
      toast({ title: 'Invitation sent', description: `Invitation sent to ${email}.` })
    } catch {
      toast({ title: 'Error', description: 'Failed to send invitation.', variant: 'destructive' })
    }
  }

  const handleChangeRole = async (userId: string, role: UserRole) => {
    try {
      await changeRoleMutation.mutateAsync({ userId, data: { role } })
      setChangeRoleMember(null)
      toast({ title: 'Role updated', description: 'Team member role has been updated.' })
    } catch {
      toast({ title: 'Error', description: 'Failed to change role.', variant: 'destructive' })
    }
  }

  const handleRemove = async () => {
    if (!removeMember) return
    try {
      await removeMutation.mutateAsync(removeMember.userId)
      setRemoveMember(null)
      toast({ title: 'Member removed', description: 'Team member has been removed.' })
    } catch {
      toast({ title: 'Error', description: 'Failed to remove team member.', variant: 'destructive' })
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Team</h1>
          <p className="text-muted-foreground">
            Manage team members and invitations for your workspace.
          </p>
        </div>
        <Button onClick={() => setIsInviteOpen(true)}>
          <Plus className="mr-2 h-4 w-4" />
          Invite Member
        </Button>
      </div>

      {/* Pending Invitations */}
      {!invitationsLoading && invitations && invitations.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Mail className="h-5 w-5" />
              Pending Invitations
            </CardTitle>
            <CardDescription>
              Invitations that have been sent but not yet accepted.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="space-y-3">
              {invitations.map((invitation) => (
                <div
                  key={invitation.invitationId}
                  className="flex items-center justify-between rounded-lg border p-3"
                >
                  <div className="space-y-1">
                    <p className="text-sm font-medium">{invitation.email}</p>
                    <div className="flex gap-2 text-xs text-muted-foreground">
                      <Badge variant="outline">{invitation.role}</Badge>
                      {invitation.isExpired ? (
                        <Badge variant="destructive">Expired</Badge>
                      ) : (
                        <span>
                          Expires {formatDistanceToNow(new Date(invitation.expiresAt), { addSuffix: true })}
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      )}

      {/* Team Members */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Users className="h-5 w-5" />
            Team Members
          </CardTitle>
          <CardDescription>
            {membersData ? `${membersData.total} member${membersData.total !== 1 ? 's' : ''}` : 'Loading...'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="mb-4">
            <Input
              placeholder="Search by name or email..."
              value={search}
              onChange={(e) => {
                setSearch(e.target.value)
                setPage(1)
              }}
              className="max-w-sm"
            />
          </div>

          {membersLoading ? (
            <div className="space-y-3">
              {[1, 2, 3].map((i) => (
                <Skeleton key={i} className="h-16 w-full" />
              ))}
            </div>
          ) : membersData?.data.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12 text-center">
              <AlertCircle className="mb-4 h-12 w-12 text-muted-foreground" />
              <h3 className="mb-2 text-lg font-semibold">No members found</h3>
              <p className="text-sm text-muted-foreground">
                {search ? 'Try a different search term.' : 'Invite your first team member to get started.'}
              </p>
            </div>
          ) : (
            <>
              <div className="space-y-2">
                {membersData?.data.map((member) => (
                  <div
                    key={member.userId}
                    className="flex items-center justify-between rounded-lg border p-4"
                  >
                    <div className="flex-1 space-y-1">
                      <div className="flex items-center gap-2">
                        <p className="font-medium">{member.name}</p>
                        <Badge variant={member.role === 'Admin' ? 'default' : 'secondary'}>
                          {member.role}
                        </Badge>
                        {member.userId === currentUser?.id && (
                          <Badge variant="outline">You</Badge>
                        )}
                      </div>
                      <div className="flex gap-4 text-xs text-muted-foreground">
                        <span>{member.email}</span>
                        {member.lastLoginAt && (
                          <span>
                            Last login: {formatDistanceToNow(new Date(member.lastLoginAt), { addSuffix: true })}
                          </span>
                        )}
                      </div>
                    </div>
                    {member.userId !== currentUser?.id && (
                      <div className="flex gap-1">
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setChangeRoleMember(member)}
                          title="Change role"
                        >
                          <UserCog className="h-4 w-4" />
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => setRemoveMember(member)}
                          title="Remove member"
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    )}
                  </div>
                ))}
              </div>

              {/* Pagination */}
              {membersData && membersData.totalPages > 1 && (
                <div className="mt-4 flex items-center justify-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={page <= 1}
                    onClick={() => setPage(page - 1)}
                  >
                    Previous
                  </Button>
                  <span className="text-sm text-muted-foreground">
                    Page {membersData.page} of {membersData.totalPages}
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={page >= membersData.totalPages}
                    onClick={() => setPage(page + 1)}
                  >
                    Next
                  </Button>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      {/* Dialogs */}
      <InviteUserDialog
        open={isInviteOpen}
        onOpenChange={setIsInviteOpen}
        onInvite={handleInvite}
        isPending={inviteMutation.isPending}
      />
      <ChangeRoleDialog
        member={changeRoleMember}
        onOpenChange={() => setChangeRoleMember(null)}
        onChangeRole={handleChangeRole}
        isPending={changeRoleMutation.isPending}
      />
      <RemoveUserDialog
        member={removeMember}
        onOpenChange={() => setRemoveMember(null)}
        onConfirm={handleRemove}
      />
    </div>
  )
}
