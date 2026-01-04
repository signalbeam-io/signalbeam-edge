/**
 * Active Rollouts Component - Shows currently in-progress rollouts
 */

import { useNavigate } from 'react-router-dom'
import { Rocket, Play, Pause, CheckCircle2, ArrowRight } from 'lucide-react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Progress } from '@/components/ui/progress'
import { Skeleton } from '@/components/ui/skeleton'
import { useActiveRollouts } from '@/hooks/api/use-phased-rollouts'
import { RolloutLifecycleStatus } from '@/api/types'
import { formatDistanceToNow } from 'date-fns'

export function ActiveRollouts() {
  const navigate = useNavigate()
  const { data: rollouts, isLoading } = useActiveRollouts()

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <Skeleton className="h-6 w-48" />
          <Skeleton className="h-4 w-64" />
        </CardHeader>
        <CardContent>
          <Skeleton className="h-20 w-full" />
        </CardContent>
      </Card>
    )
  }

  const activeRollouts = rollouts || []

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <Rocket className="h-5 w-5" />
              Active Rollouts
            </CardTitle>
            <CardDescription>
              {activeRollouts.length} rollout{activeRollouts.length !== 1 ? 's' : ''} in progress
            </CardDescription>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => navigate('/phased-rollouts')}
          >
            View All
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        </div>
      </CardHeader>
      <CardContent>
        {activeRollouts.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-8 text-center">
            <Rocket className="mb-2 h-12 w-12 text-muted-foreground" />
            <p className="text-sm text-muted-foreground">No active rollouts</p>
          </div>
        ) : (
          <div className="space-y-4">
            {activeRollouts.slice(0, 5).map((rollout) => {
              const progress = rollout.currentPhaseSuccessRate * 100

              return (
                <div
                  key={rollout.rolloutId}
                  className="flex flex-col gap-2 rounded-lg border p-3 transition-colors hover:bg-muted/50 cursor-pointer"
                  onClick={() => navigate(`/phased-rollouts/${rollout.rolloutId}`)}
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <h4 className="font-medium text-sm">{rollout.name}</h4>
                      {renderStatusBadge(rollout.status as RolloutLifecycleStatus)}
                    </div>
                    <span className="text-xs text-muted-foreground">
                      {formatDistanceToNow(new Date(rollout.startedAt), { addSuffix: true })}
                    </span>
                  </div>
                  <div className="flex items-center gap-2 text-xs text-muted-foreground">
                    <span>
                      Phase {rollout.currentPhaseNumber}: {rollout.currentPhaseName}
                    </span>
                    <span>•</span>
                    <span>
                      {rollout.currentPhaseSuccessCount} / {rollout.currentPhaseTargetCount} devices
                    </span>
                  </div>
                  <Progress value={progress} className="h-2" />
                </div>
              )
            })}
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function renderStatusBadge(status: RolloutLifecycleStatus) {
  const variants: Record<RolloutLifecycleStatus, { color: string; icon: any }> = {
    Pending: { color: 'bg-gray-100 text-gray-800', icon: null },
    InProgress: { color: 'bg-blue-100 text-blue-800', icon: Play },
    Paused: { color: 'bg-yellow-100 text-yellow-800', icon: Pause },
    Completed: { color: 'bg-green-100 text-green-800', icon: CheckCircle2 },
    RolledBack: { color: 'bg-orange-100 text-orange-800', icon: null },
    Failed: { color: 'bg-red-100 text-red-800', icon: null },
  }

  const { color, icon: Icon } = variants[status]
  return (
    <Badge className={`${color} text-xs`}>
      {Icon && <Icon className="mr-1 h-3 w-3" />}
      {status}
    </Badge>
  )
}
