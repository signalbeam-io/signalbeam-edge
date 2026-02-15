/**
 * Public page for accepting or declining a team invitation via token.
 */

import { useState, useEffect } from 'react'
import { useSearchParams, useNavigate } from 'react-router-dom'
import { CheckCircle, XCircle, Loader2, AlertCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { teamApi } from '@/api/services/team.api'

export function AcceptInvitationPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const token = searchParams.get('token')

  const [status, setStatus] = useState<'idle' | 'accepting' | 'declining' | 'accepted' | 'declined' | 'error'>('idle')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!token) {
      setStatus('error')
      setError('Invalid invitation link. No token provided.')
    }
  }, [token])

  const handleAccept = async () => {
    if (!token) return
    setStatus('accepting')
    try {
      await teamApi.acceptInvitation(token)
      setStatus('accepted')
    } catch {
      setStatus('error')
      setError('Failed to accept invitation. It may have expired or already been used.')
    }
  }

  const handleDecline = async () => {
    if (!token) return
    setStatus('declining')
    try {
      await teamApi.declineInvitation(token)
      setStatus('declined')
    } catch {
      setStatus('error')
      setError('Failed to decline invitation. It may have expired or already been used.')
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
      <Card className="w-full max-w-md">
        {status === 'idle' && token && (
          <>
            <CardHeader>
              <CardTitle>Team Invitation</CardTitle>
              <CardDescription>
                You have been invited to join a team on SignalBeam Edge.
              </CardDescription>
            </CardHeader>
            <CardFooter className="flex gap-3">
              <Button onClick={handleAccept} className="flex-1">
                Accept Invitation
              </Button>
              <Button variant="outline" onClick={handleDecline} className="flex-1">
                Decline
              </Button>
            </CardFooter>
          </>
        )}

        {(status === 'accepting' || status === 'declining') && (
          <CardContent className="flex flex-col items-center py-12">
            <Loader2 className="mb-4 h-12 w-12 animate-spin text-muted-foreground" />
            <p className="text-sm text-muted-foreground">
              {status === 'accepting' ? 'Accepting invitation...' : 'Declining invitation...'}
            </p>
          </CardContent>
        )}

        {status === 'accepted' && (
          <>
            <CardContent className="flex flex-col items-center py-12">
              <CheckCircle className="mb-4 h-12 w-12 text-green-500" />
              <h3 className="mb-2 text-lg font-semibold">Invitation Accepted</h3>
              <p className="text-center text-sm text-muted-foreground">
                You have joined the team. Sign in to access the dashboard.
              </p>
            </CardContent>
            <CardFooter>
              <Button onClick={() => navigate('/login')} className="w-full">
                Go to Sign In
              </Button>
            </CardFooter>
          </>
        )}

        {status === 'declined' && (
          <CardContent className="flex flex-col items-center py-12">
            <XCircle className="mb-4 h-12 w-12 text-muted-foreground" />
            <h3 className="mb-2 text-lg font-semibold">Invitation Declined</h3>
            <p className="text-center text-sm text-muted-foreground">
              You have declined this invitation.
            </p>
          </CardContent>
        )}

        {status === 'error' && (
          <>
            <CardContent className="flex flex-col items-center py-12">
              <AlertCircle className="mb-4 h-12 w-12 text-destructive" />
              <h3 className="mb-2 text-lg font-semibold">Something went wrong</h3>
              <p className="text-center text-sm text-muted-foreground">
                {error}
              </p>
            </CardContent>
            <CardFooter>
              <Button variant="outline" onClick={() => navigate('/login')} className="w-full">
                Go to Sign In
              </Button>
            </CardFooter>
          </>
        )}
      </Card>
    </div>
  )
}
