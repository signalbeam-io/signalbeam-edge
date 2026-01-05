/**
 * Registration Tokens Management Page
 */

import { useState, type FormEvent } from 'react'
import { Plus, Copy, Trash2, CheckCircle2, XCircle, AlertCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
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
import { Label } from '@/components/ui/label'
import { Input } from '@/components/ui/input'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { useToast } from '@/hooks/use-toast'
import {
  useRegistrationTokens,
  useCreateRegistrationToken,
  useRevokeRegistrationToken,
} from '@/hooks/api/use-registration-tokens'
import { formatDistanceToNow } from 'date-fns'
import { getTenantId } from '@/lib/tenant'
import { Skeleton } from '@/components/ui/skeleton'

export function RegistrationTokensPage() {
  const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false)
  const [revokeTokenId, setRevokeTokenId] = useState<string | null>(null)
  const [generatedToken, setGeneratedToken] = useState<string | null>(null)
  const { toast } = useToast()

  const { data: tokensData, isLoading } = useRegistrationTokens({ page: 1, pageSize: 50 })
  const createMutation = useCreateRegistrationToken()
  const revokeMutation = useRevokeRegistrationToken()

  const handleCreateToken = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    const formData = new FormData(e.currentTarget)

    const maxUsesValue = formData.get('maxUses') as string
    const expiresInDays = formData.get('expiresInDays') as string

    let expiresAt: string | null = null
    if (expiresInDays && parseInt(expiresInDays) > 0) {
      const date = new Date()
      date.setDate(date.getDate() + parseInt(expiresInDays))
      expiresAt = date.toISOString()
    }

    try {
      const result = await createMutation.mutateAsync({
        tenantId: getTenantId(),
        maxUses: maxUsesValue ? parseInt(maxUsesValue) : null,
        expiresAt,
      })

      setGeneratedToken(result.token)
      toast({
        title: 'Token created',
        description: 'Registration token has been generated successfully.',
      })
    } catch {
      toast({
        title: 'Error',
        description: 'Failed to create registration token.',
        variant: 'destructive',
      })
    }
  }

  const handleRevokeToken = async () => {
    if (!revokeTokenId) return

    try {
      await revokeMutation.mutateAsync(revokeTokenId)
      toast({
        title: 'Token revoked',
        description: 'Registration token has been revoked.',
      })
      setRevokeTokenId(null)
    } catch {
      toast({
        title: 'Error',
        description: 'Failed to revoke registration token.',
        variant: 'destructive',
      })
    }
  }

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text)
    toast({
      title: 'Copied to clipboard',
      description: 'Token copied successfully',
    })
  }

  const handleCloseCreateDialog = () => {
    setIsCreateDialogOpen(false)
    setGeneratedToken(null)
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Registration Tokens</h1>
          <p className="text-muted-foreground">
            Generate tokens for device registration and manage existing tokens.
          </p>
        </div>
        <Dialog open={isCreateDialogOpen} onOpenChange={setIsCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              Generate Token
            </Button>
          </DialogTrigger>
          <DialogContent className="sm:max-w-[500px]">
            {generatedToken ? (
              <>
                <DialogHeader>
                  <DialogTitle>Token Generated Successfully</DialogTitle>
                  <DialogDescription>
                    Copy this token and use it to register devices. This token will not be shown
                    again.
                  </DialogDescription>
                </DialogHeader>
                <div className="py-4">
                  <Alert>
                    <CheckCircle2 className="h-4 w-4" />
                    <AlertDescription>
                      <div className="mt-2 flex items-center gap-2 rounded-md bg-muted p-3 font-mono text-sm break-all">
                        <code className="flex-1">{generatedToken}</code>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => copyToClipboard(generatedToken)}
                        >
                          <Copy className="h-3 w-3" />
                        </Button>
                      </div>
                    </AlertDescription>
                  </Alert>
                </div>
                <DialogFooter>
                  <Button type="button" onClick={handleCloseCreateDialog}>
                    Done
                  </Button>
                </DialogFooter>
              </>
            ) : (
              <>
                <DialogHeader>
                  <DialogTitle>Generate Registration Token</DialogTitle>
                  <DialogDescription>
                    Create a new token for device registration. You can optionally set limits on
                    usage and expiration.
                  </DialogDescription>
                </DialogHeader>
                <form onSubmit={handleCreateToken}>
                  <div className="space-y-4 py-4">
                    <div className="space-y-2">
                      <Label htmlFor="maxUses">Maximum Uses (optional)</Label>
                      <Input
                        id="maxUses"
                        name="maxUses"
                        type="number"
                        min="1"
                        placeholder="Unlimited"
                      />
                      <p className="text-xs text-muted-foreground">
                        Leave empty for unlimited uses
                      </p>
                    </div>
                    <div className="space-y-2">
                      <Label htmlFor="expiresInDays">Expires In (days, optional)</Label>
                      <Input
                        id="expiresInDays"
                        name="expiresInDays"
                        type="number"
                        min="1"
                        placeholder="Never expires"
                      />
                      <p className="text-xs text-muted-foreground">
                        Leave empty for tokens that never expire
                      </p>
                    </div>
                  </div>
                  <DialogFooter>
                    <Button type="button" variant="outline" onClick={handleCloseCreateDialog}>
                      Cancel
                    </Button>
                    <Button type="submit" disabled={createMutation.isPending}>
                      {createMutation.isPending ? 'Generating...' : 'Generate Token'}
                    </Button>
                  </DialogFooter>
                </form>
              </>
            )}
          </DialogContent>
        </Dialog>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Active Tokens</CardTitle>
          <CardDescription>
            Manage registration tokens for your device fleet. Revoked or expired tokens cannot be
            used to register new devices.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-3">
              {[1, 2, 3].map((i) => (
                <Skeleton key={i} className="h-20 w-full" />
              ))}
            </div>
          ) : tokensData?.data.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12 text-center">
              <AlertCircle className="mb-4 h-12 w-12 text-muted-foreground" />
              <h3 className="mb-2 text-lg font-semibold">No tokens yet</h3>
              <p className="mb-4 text-sm text-muted-foreground">
                Generate your first registration token to start registering devices.
              </p>
              <Button onClick={() => setIsCreateDialogOpen(true)}>
                <Plus className="mr-2 h-4 w-4" />
                Generate Token
              </Button>
            </div>
          ) : (
            <div className="space-y-3">
              {tokensData?.data.map((token) => (
                <div
                  key={token.id}
                  className="flex items-center justify-between rounded-lg border p-4"
                >
                  <div className="flex-1 space-y-1">
                    <div className="flex items-center gap-2">
                      <code className="rounded bg-muted px-2 py-1 font-mono text-sm">
                        {token.token.substring(0, 16)}...
                      </code>
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => copyToClipboard(token.token)}
                      >
                        <Copy className="h-3 w-3" />
                      </Button>
                      {token.isActive ? (
                        <Badge variant="default">
                          <CheckCircle2 className="mr-1 h-3 w-3" />
                          Active
                        </Badge>
                      ) : (
                        <Badge variant="secondary">
                          <XCircle className="mr-1 h-3 w-3" />
                          Inactive
                        </Badge>
                      )}
                    </div>
                    <div className="flex gap-4 text-xs text-muted-foreground">
                      <span>
                        Uses: {token.currentUses}
                        {token.maxUses ? ` / ${token.maxUses}` : ' (unlimited)'}
                      </span>
                      {token.expiresAt && (
                        <span>
                          Expires: {formatDistanceToNow(new Date(token.expiresAt), { addSuffix: true })}
                        </span>
                      )}
                      <span>
                        Created: {formatDistanceToNow(new Date(token.createdAt), { addSuffix: true })}
                      </span>
                    </div>
                  </div>
                  {token.isActive && (
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => setRevokeTokenId(token.id)}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  )}
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <AlertDialog open={!!revokeTokenId} onOpenChange={() => setRevokeTokenId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Revoke Registration Token?</AlertDialogTitle>
            <AlertDialogDescription>
              This action will permanently revoke this token. It will no longer be usable for device
              registration. This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleRevokeToken}>Revoke Token</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
