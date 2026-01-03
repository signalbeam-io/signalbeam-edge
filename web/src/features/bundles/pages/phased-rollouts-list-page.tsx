/**
 * Phased Rollouts List Page
 */

import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Eye, Rocket } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { usePhasedRollouts } from '@/hooks/api/use-phased-rollouts'
import type { RolloutLifecycleStatus } from '@/api/types'

export function PhasedRolloutsListPage() {
  const navigate = useNavigate()
  const [page, setPage] = useState(1)
  const pageSize = 20

  const { data, isLoading } = usePhasedRollouts({ page, pageSize })

  const rollouts = data?.data || []
  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 0

  return (
    <div className="container mx-auto py-8 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Phased Rollouts</h1>
          <p className="text-muted-foreground">Manage multi-phase bundle deployments</p>
        </div>
      </div>

      {/* Rollouts Table */}
      <Card>
        <CardHeader>
          <CardTitle>All Rollouts</CardTitle>
          <CardDescription>
            {data?.total || 0} rollout(s) total
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="text-center py-8">Loading rollouts...</div>
          ) : rollouts.length === 0 ? (
            <div className="text-center py-8">
              <Rocket className="mx-auto h-12 w-12 text-muted-foreground mb-4" />
              <p className="text-muted-foreground">
                No phased rollouts found. Create one from a bundle detail page.
              </p>
            </div>
          ) : (
            <div className="rounded-md border">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Name</TableHead>
                    <TableHead>Bundle</TableHead>
                    <TableHead>Version</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Phases</TableHead>
                    <TableHead>Current Phase</TableHead>
                    <TableHead>Progress</TableHead>
                    <TableHead>Created</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rollouts.map((rollout) => {
                    const currentPhase = rollout.phases.find(
                      (p) => p.phaseNumber === rollout.currentPhaseNumber
                    )
                    const completedPhases = rollout.phases.filter(
                      (p) => p.status === 'Completed'
                    ).length
                    const progressPercent = Math.round(
                      (completedPhases / rollout.phases.length) * 100
                    )

                    return (
                      <TableRow
                        key={rollout.id}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => navigate(`/phased-rollouts/${rollout.id}`)}
                      >
                        <TableCell className="font-medium">{rollout.name}</TableCell>
                        <TableCell>
                          <span className="font-mono text-xs">{rollout.bundleId.slice(0, 8)}</span>
                        </TableCell>
                        <TableCell>{rollout.targetVersion}</TableCell>
                        <TableCell>{renderStatusBadge(rollout.status)}</TableCell>
                        <TableCell>{rollout.phases.length}</TableCell>
                        <TableCell>
                          {currentPhase ? (
                            <span className="text-sm">
                              {currentPhase.name} ({currentPhase.phaseNumber})
                            </span>
                          ) : (
                            <span className="text-sm text-muted-foreground">—</span>
                          )}
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <div className="w-24 h-2 bg-gray-200 rounded-full overflow-hidden">
                              <div
                                className="h-full bg-primary transition-all"
                                style={{ width: `${progressPercent}%` }}
                              />
                            </div>
                            <span className="text-xs text-muted-foreground">
                              {progressPercent}%
                            </span>
                          </div>
                        </TableCell>
                        <TableCell>
                          <span className="text-sm text-muted-foreground">
                            {new Date(rollout.createdAt).toLocaleDateString()}
                          </span>
                        </TableCell>
                        <TableCell className="text-right">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={(e) => {
                              e.stopPropagation()
                              navigate(`/phased-rollouts/${rollout.id}`)
                            }}
                          >
                            <Eye className="h-4 w-4" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    )
                  })}
                </TableBody>
              </Table>
            </div>
          )}

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between mt-4">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                Previous
              </Button>
              <span className="text-sm text-muted-foreground">
                Page {page} of {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
              >
                Next
              </Button>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function renderStatusBadge(status: RolloutLifecycleStatus) {
  const variants: Record<RolloutLifecycleStatus, string> = {
    Pending: 'bg-gray-100 text-gray-800',
    InProgress: 'bg-blue-100 text-blue-800',
    Paused: 'bg-yellow-100 text-yellow-800',
    Completed: 'bg-green-100 text-green-800',
    RolledBack: 'bg-orange-100 text-orange-800',
    Failed: 'bg-red-100 text-red-800',
  }

  return <Badge className={variants[status]}>{status}</Badge>
}
