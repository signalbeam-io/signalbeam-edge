/**
 * Phased Rollout Detail Page
 */

import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, Play, Pause, RotateCcw, CheckCircle2, Clock, AlertCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import {
  usePhasedRollout,
  useStartPhasedRollout,
  usePausePhasedRollout,
  useResumePhasedRollout,
  useRollbackPhasedRollout,
} from '@/hooks/api/use-phased-rollouts'
import { useToast } from '@/hooks/use-toast'
import type { RolloutLifecycleStatus, PhaseStatus, AssignmentStatus } from '@/api/types'

export function PhasedRolloutDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { toast } = useToast()

  const { data: rollout, isLoading } = usePhasedRollout(id!)
  const startRollout = useStartPhasedRollout()
  const pauseRollout = usePausePhasedRollout()
  const resumeRollout = useResumePhasedRollout()
  const rollbackRollout = useRollbackPhasedRollout()

  const handleStart = async () => {
    try {
      await startRollout.mutateAsync(id!)
      toast({ title: 'Rollout started', description: 'The rollout has been started.' })
    } catch (error) {
      toast({
        title: 'Failed to start rollout',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    }
  }

  const handlePause = async () => {
    try {
      await pauseRollout.mutateAsync(id!)
      toast({ title: 'Rollout paused', description: 'The rollout has been paused.' })
    } catch (error) {
      toast({
        title: 'Failed to pause rollout',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    }
  }

  const handleResume = async () => {
    try {
      await resumeRollout.mutateAsync(id!)
      toast({ title: 'Rollout resumed', description: 'The rollout has been resumed.' })
    } catch (error) {
      toast({
        title: 'Failed to resume rollout',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    }
  }

  const handleRollback = async () => {
    if (
      !confirm(
        'Are you sure you want to rollback? This will revert all devices to the previous version.'
      )
    ) {
      return
    }

    try {
      await rollbackRollout.mutateAsync(id!)
      toast({
        title: 'Rollout rolled back',
        description: 'Devices are being reverted to the previous version.',
      })
    } catch (error) {
      toast({
        title: 'Failed to rollback',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    }
  }

  if (isLoading) {
    return (
      <div className="container mx-auto py-8">
        <div className="text-center">Loading rollout details...</div>
      </div>
    )
  }

  if (!rollout) {
    return (
      <div className="container mx-auto py-8">
        <div className="text-center">Rollout not found</div>
      </div>
    )
  }

  const currentPhase = rollout.phases.find((p) => p.phaseNumber === rollout.currentPhaseNumber)
  const overallProgress = calculateOverallProgress(rollout)

  return (
    <div className="container mx-auto py-8 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => navigate('/phased-rollouts')}>
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <div>
            <h1 className="text-3xl font-bold">{rollout.name}</h1>
            <p className="text-muted-foreground">
              {rollout.description || `Bundle ${rollout.bundleId} → v${rollout.targetVersion}`}
            </p>
          </div>
        </div>
        <div className="flex gap-2">
          {rollout.status === 'Pending' && (
            <Button onClick={handleStart} disabled={startRollout.isPending}>
              <Play className="mr-2 h-4 w-4" />
              Start Rollout
            </Button>
          )}
          {rollout.status === 'InProgress' && (
            <Button onClick={handlePause} variant="outline" disabled={pauseRollout.isPending}>
              <Pause className="mr-2 h-4 w-4" />
              Pause
            </Button>
          )}
          {rollout.status === 'Paused' && (
            <Button onClick={handleResume} disabled={resumeRollout.isPending}>
              <Play className="mr-2 h-4 w-4" />
              Resume
            </Button>
          )}
          {(rollout.status === 'InProgress' || rollout.status === 'Paused') &&
            rollout.previousVersion && (
              <Button
                onClick={handleRollback}
                variant="destructive"
                disabled={rollbackRollout.isPending}
              >
                <RotateCcw className="mr-2 h-4 w-4" />
                Rollback
              </Button>
            )}
        </div>
      </div>

      {/* Status Overview */}
      <Card>
        <CardHeader>
          <CardTitle>Rollout Status</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div>
              <div className="text-sm text-muted-foreground">Status</div>
              <div className="mt-1">{renderStatusBadge(rollout.status)}</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground">Current Phase</div>
              <div className="mt-1 font-medium">
                {currentPhase ? `${currentPhase.name} (${currentPhase.phaseNumber})` : 'None'}
              </div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground">Progress</div>
              <div className="mt-1 font-medium">{overallProgress}%</div>
            </div>
            <div>
              <div className="text-sm text-muted-foreground">Failure Threshold</div>
              <div className="mt-1 font-medium">{(rollout.failureThreshold * 100).toFixed(1)}%</div>
            </div>
          </div>
          <div>
            <Progress value={overallProgress} className="h-2" />
          </div>
        </CardContent>
      </Card>

      {/* Phases Timeline */}
      <Card>
        <CardHeader>
          <CardTitle>Rollout Phases</CardTitle>
          <CardDescription>
            {rollout.phases.length} phase(s) configured for this rollout
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {rollout.phases
              .sort((a, b) => a.phaseNumber - b.phaseNumber)
              .map((phase) => {
                const isCurrentPhase = phase.phaseNumber === rollout.currentPhaseNumber
                const successRate =
                  phase.successCount + phase.failureCount > 0
                    ? (phase.successCount / (phase.successCount + phase.failureCount)) * 100
                    : 0

                return (
                  <div
                    key={phase.id}
                    className={`p-4 rounded-lg border ${isCurrentPhase ? 'border-primary bg-primary/5' : ''}`}
                  >
                    <div className="flex items-start justify-between">
                      <div className="flex-1">
                        <div className="flex items-center gap-3">
                          <div className="flex items-center justify-center w-8 h-8 rounded-full bg-primary text-primary-foreground text-sm font-medium">
                            {phase.phaseNumber}
                          </div>
                          <div>
                            <h4 className="font-medium">{phase.name}</h4>
                            <p className="text-sm text-muted-foreground">
                              Target: {phase.targetPercentage}%
                              {phase.targetDeviceCount && ` (${phase.targetDeviceCount} devices)`}
                              {phase.minHealthyDuration &&
                                ` • Min healthy: ${formatDuration(phase.minHealthyDuration)}`}
                            </p>
                          </div>
                        </div>

                        <div className="mt-4 space-y-2">
                          <div className="flex items-center justify-between text-sm">
                            <span className="text-muted-foreground">Status</span>
                            {renderPhaseStatusBadge(phase.status)}
                          </div>

                          {phase.deviceAssignments.length > 0 && (
                            <>
                              <div className="flex items-center justify-between text-sm">
                                <span className="text-muted-foreground">Devices</span>
                                <span className="font-medium">{phase.deviceAssignments.length}</span>
                              </div>
                              <div className="flex items-center justify-between text-sm">
                                <span className="text-muted-foreground">Success Rate</span>
                                <span className="font-medium">{successRate.toFixed(1)}%</span>
                              </div>
                              <div className="flex items-center justify-between text-sm">
                                <span className="text-muted-foreground">Success / Failure</span>
                                <span className="font-medium">
                                  {phase.successCount} / {phase.failureCount}
                                </span>
                              </div>
                            </>
                          )}

                          {phase.startedAt && (
                            <div className="flex items-center justify-between text-sm">
                              <span className="text-muted-foreground">Started</span>
                              <span>{new Date(phase.startedAt).toLocaleString()}</span>
                            </div>
                          )}
                          {phase.completedAt && (
                            <div className="flex items-center justify-between text-sm">
                              <span className="text-muted-foreground">Completed</span>
                              <span>{new Date(phase.completedAt).toLocaleString()}</span>
                            </div>
                          )}
                        </div>

                        {/* Device Assignments */}
                        {phase.deviceAssignments.length > 0 && (
                          <div className="mt-4">
                            <h5 className="text-sm font-medium mb-2">Device Assignments</h5>
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                              {phase.deviceAssignments.map((assignment) => (
                                <div
                                  key={assignment.id}
                                  className="flex items-center justify-between p-2 rounded border text-sm"
                                >
                                  <span className="font-mono text-xs truncate">
                                    {assignment.deviceId}
                                  </span>
                                  {renderAssignmentStatusBadge(assignment.status)}
                                </div>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                )
              })}
          </div>
        </CardContent>
      </Card>
    </div>
  )
}

function renderStatusBadge(status: RolloutLifecycleStatus) {
  const variants: Record<RolloutLifecycleStatus, { color: string; icon: any }> = {
    Pending: { color: 'bg-gray-100 text-gray-800', icon: Clock },
    InProgress: { color: 'bg-blue-100 text-blue-800', icon: Play },
    Paused: { color: 'bg-yellow-100 text-yellow-800', icon: Pause },
    Completed: { color: 'bg-green-100 text-green-800', icon: CheckCircle2 },
    RolledBack: { color: 'bg-orange-100 text-orange-800', icon: RotateCcw },
    Failed: { color: 'bg-red-100 text-red-800', icon: AlertCircle },
  }

  const { color, icon: Icon } = variants[status]
  return (
    <Badge className={color}>
      <Icon className="mr-1 h-3 w-3" />
      {status}
    </Badge>
  )
}

function renderPhaseStatusBadge(status: PhaseStatus) {
  const variants: Record<PhaseStatus, string> = {
    Pending: 'bg-gray-100 text-gray-800',
    InProgress: 'bg-blue-100 text-blue-800',
    Completed: 'bg-green-100 text-green-800',
    Failed: 'bg-red-100 text-red-800',
  }

  return <Badge className={variants[status]}>{status}</Badge>
}

function renderAssignmentStatusBadge(status: AssignmentStatus) {
  const variants: Record<AssignmentStatus, string> = {
    Pending: 'bg-gray-100 text-gray-800',
    Assigned: 'bg-blue-100 text-blue-800',
    Reconciling: 'bg-yellow-100 text-yellow-800',
    Succeeded: 'bg-green-100 text-green-800',
    Failed: 'bg-red-100 text-red-800',
  }

  return (
    <Badge className={`${variants[status]} text-xs`} variant="outline">
      {status}
    </Badge>
  )
}

function calculateOverallProgress(rollout: any): number {
  const totalPhases = rollout.phases.length
  if (totalPhases === 0) return 0

  const completedPhases = rollout.phases.filter((p: any) => p.status === 'Completed').length
  return Math.round((completedPhases / totalPhases) * 100)
}

function formatDuration(iso8601Duration: string): string {
  // Simple ISO 8601 duration parser for minutes
  // Format: PT5M, PT10M, PT30M, etc.
  const match = iso8601Duration.match(/PT(\d+)M/)
  if (match) {
    return `${match[1]} min`
  }
  return iso8601Duration
}
