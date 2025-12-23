/**
 * Rollout Status Page
 *
 * Displays the status of a bundle rollout across devices
 */

import { useParams, useNavigate } from 'react-router-dom'
import { formatDistanceToNow } from 'date-fns'
import {
  ArrowLeft,
  Package,
  Users,
  RefreshCw,
  CheckCircle2,
  XCircle,
  Clock,
  Loader2,
  AlertCircle,
  Monitor,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Progress } from '@/components/ui/progress'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useRollout, useDeviceRolloutStatus, useCancelRollout, useRetryFailedDevices } from '@/hooks/api/use-rollouts'
import { DeviceRolloutState } from '@/api/types'
import { useToast } from '@/hooks/use-toast'

export function RolloutStatusPage() {
  const { rolloutId } = useParams<{ rolloutId: string }>()
  const navigate = useNavigate()
  const { toast } = useToast()

  const { data: rollout, isLoading, isError, refetch } = useRollout(rolloutId || '', !!rolloutId)
  const { data: deviceStatuses, isLoading: deviceStatusesLoading } = useDeviceRolloutStatus(
    rolloutId || '',
    !!rolloutId
  )

  const cancelRollout = useCancelRollout()
  const retryFailedDevices = useRetryFailedDevices()

  const handleRefresh = () => {
    refetch()
  }

  const handleCancel = async () => {
    if (!rolloutId) return

    try {
      await cancelRollout.mutateAsync(rolloutId)
      toast({
        title: 'Rollout cancelled',
        description: 'The rollout has been cancelled successfully.',
      })
    } catch (error) {
      toast({
        title: 'Failed to cancel rollout',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    }
  }

  const handleRetry = async () => {
    if (!rolloutId) return

    try {
      await retryFailedDevices.mutateAsync(rolloutId)
      toast({
        title: 'Retrying failed devices',
        description: 'Failed devices will be retried.',
      })
    } catch (error) {
      toast({
        title: 'Failed to retry devices',
        description: error instanceof Error ? error.message : 'An error occurred',
        variant: 'destructive',
      })
    }
  }

  if (isLoading) {
    return (
      <div className="container mx-auto space-y-6 py-6">
        <Skeleton className="h-10 w-64" />
        <div className="grid gap-4 md:grid-cols-4">
          <Skeleton className="h-32" />
          <Skeleton className="h-32" />
          <Skeleton className="h-32" />
          <Skeleton className="h-32" />
        </div>
        <Skeleton className="h-96 w-full" />
      </div>
    )
  }

  if (isError || !rollout) {
    return (
      <div className="container mx-auto py-6">
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <Package className="mb-4 h-12 w-12 text-muted-foreground" />
            <p className="text-lg font-medium">Rollout not found</p>
            <Button onClick={() => navigate('/bundles')} className="mt-4">
              Back to Bundles
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  const progress = rollout.progress
  const progressPercentage = progress.total > 0
    ? ((progress.succeeded + progress.failed) / progress.total) * 100
    : 0

  const isActive = rollout.status === 'pending' || rollout.status === 'in_progress'
  const hasFailedDevices = progress.failed > 0

  // Group devices by status
  const devicesByStatus = {
    pending: deviceStatuses?.filter((d) => d.status === DeviceRolloutState.Pending) || [],
    updating: deviceStatuses?.filter((d) => d.status === DeviceRolloutState.Updating) || [],
    succeeded: deviceStatuses?.filter((d) => d.status === DeviceRolloutState.Succeeded) || [],
    failed: deviceStatuses?.filter((d) => d.status === DeviceRolloutState.Failed) || [],
  }

  return (
    <div className="container mx-auto space-y-6 py-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="sm" onClick={() => navigate('/bundles')}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
          <div>
            <h1 className="text-3xl font-bold">Rollout Status</h1>
            <p className="text-sm text-muted-foreground">
              Track deployment progress across your fleet
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={handleRefresh}>
            <RefreshCw className="h-4 w-4" />
          </Button>
          {hasFailedDevices && (
            <Button
              variant="outline"
              onClick={handleRetry}
              disabled={retryFailedDevices.isPending}
            >
              {retryFailedDevices.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="mr-2 h-4 w-4" />
              )}
              Retry Failed
            </Button>
          )}
          {isActive && (
            <Button
              variant="destructive"
              onClick={handleCancel}
              disabled={cancelRollout.isPending}
            >
              {cancelRollout.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <XCircle className="mr-2 h-4 w-4" />
              )}
              Cancel Rollout
            </Button>
          )}
        </div>
      </div>

      {/* Bundle Info */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <Package className="h-8 w-8 text-muted-foreground" />
              <div>
                <CardTitle className="text-2xl">
                  Bundle: {rollout.bundleId}
                </CardTitle>
                <CardDescription>
                  Version {rollout.version} • Started{' '}
                  {formatDistanceToNow(new Date(rollout.createdAt), { addSuffix: true })}
                </CardDescription>
              </div>
            </div>
            <RolloutStatusBadge status={rollout.status} />
          </div>
        </CardHeader>
      </Card>

      {/* Progress Stats */}
      <div className="grid gap-4 md:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Devices</CardTitle>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{progress.total}</div>
            <p className="text-xs text-muted-foreground">
              Target {rollout.targetType === 'group' ? 'groups' : 'devices'}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Succeeded</CardTitle>
            <CheckCircle2 className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-green-600">{progress.succeeded}</div>
            <p className="text-xs text-muted-foreground">
              {progress.total > 0 ? ((progress.succeeded / progress.total) * 100).toFixed(1) : 0}% complete
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">In Progress</CardTitle>
            <Loader2 className="h-4 w-4 text-blue-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-blue-600">{progress.inProgress}</div>
            <p className="text-xs text-muted-foreground">
              {progress.pending} pending
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Failed</CardTitle>
            <XCircle className="h-4 w-4 text-red-600" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-red-600">{progress.failed}</div>
            <p className="text-xs text-muted-foreground">
              {progress.total > 0 ? ((progress.failed / progress.total) * 100).toFixed(1) : 0}% failed
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Progress Bar */}
      <Card>
        <CardHeader>
          <CardTitle>Rollout Progress</CardTitle>
          <CardDescription>
            {progressPercentage.toFixed(1)}% of devices have completed the update
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <Progress value={progressPercentage} className="h-4" />
          <div className="grid grid-cols-4 gap-2 text-sm">
            <div className="flex items-center gap-2">
              <div className="h-3 w-3 rounded-full bg-gray-400" />
              <span className="text-muted-foreground">Pending: {progress.pending}</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="h-3 w-3 rounded-full bg-blue-500" />
              <span className="text-muted-foreground">Updating: {progress.inProgress}</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="h-3 w-3 rounded-full bg-green-500" />
              <span className="text-muted-foreground">Succeeded: {progress.succeeded}</span>
            </div>
            <div className="flex items-center gap-2">
              <div className="h-3 w-3 rounded-full bg-red-500" />
              <span className="text-muted-foreground">Failed: {progress.failed}</span>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Device Status Tabs */}
      <Tabs defaultValue="all" className="space-y-4">
        <TabsList>
          <TabsTrigger value="all">
            All Devices
            <Badge variant="secondary" className="ml-2">
              {progress.total}
            </Badge>
          </TabsTrigger>
          {progress.pending > 0 && (
            <TabsTrigger value="pending">
              Pending
              <Badge variant="secondary" className="ml-2">
                {progress.pending}
              </Badge>
            </TabsTrigger>
          )}
          {progress.inProgress > 0 && (
            <TabsTrigger value="updating">
              Updating
              <Badge variant="secondary" className="ml-2">
                {progress.inProgress}
              </Badge>
            </TabsTrigger>
          )}
          {progress.succeeded > 0 && (
            <TabsTrigger value="succeeded">
              Succeeded
              <Badge variant="secondary" className="ml-2">
                {progress.succeeded}
              </Badge>
            </TabsTrigger>
          )}
          {progress.failed > 0 && (
            <TabsTrigger value="failed">
              Failed
              <Badge variant="secondary" className="ml-2">
                {progress.failed}
              </Badge>
            </TabsTrigger>
          )}
        </TabsList>

        <TabsContent value="all">
          <DeviceStatusTable devices={deviceStatuses || []} loading={deviceStatusesLoading} />
        </TabsContent>

        <TabsContent value="pending">
          <DeviceStatusTable devices={devicesByStatus.pending} loading={deviceStatusesLoading} />
        </TabsContent>

        <TabsContent value="updating">
          <DeviceStatusTable devices={devicesByStatus.updating} loading={deviceStatusesLoading} />
        </TabsContent>

        <TabsContent value="succeeded">
          <DeviceStatusTable devices={devicesByStatus.succeeded} loading={deviceStatusesLoading} />
        </TabsContent>

        <TabsContent value="failed">
          <DeviceStatusTable
            devices={devicesByStatus.failed}
            loading={deviceStatusesLoading}
            showError
          />
        </TabsContent>
      </Tabs>
    </div>
  )
}

function RolloutStatusBadge({ status }: { status: string }) {
  switch (status) {
    case 'pending':
      return (
        <Badge variant="outline" className="gap-1">
          <Clock className="h-3 w-3" />
          Pending
        </Badge>
      )
    case 'in_progress':
      return (
        <Badge variant="outline" className="gap-1 border-blue-500 text-blue-600">
          <Loader2 className="h-3 w-3 animate-spin" />
          In Progress
        </Badge>
      )
    case 'completed':
      return (
        <Badge variant="outline" className="gap-1 border-green-500 text-green-600">
          <CheckCircle2 className="h-3 w-3" />
          Completed
        </Badge>
      )
    case 'failed':
      return (
        <Badge variant="outline" className="gap-1 border-red-500 text-red-600">
          <XCircle className="h-3 w-3" />
          Failed
        </Badge>
      )
    case 'cancelled':
      return (
        <Badge variant="outline" className="gap-1">
          <XCircle className="h-3 w-3" />
          Cancelled
        </Badge>
      )
    default:
      return <Badge variant="outline">{status}</Badge>
  }
}

interface DeviceStatusTableProps {
  devices: Array<{
    deviceId: string
    deviceName: string
    status: DeviceRolloutState
    startedAt: string | null
    completedAt: string | null
    error: string | null
  }>
  loading: boolean
  showError?: boolean
}

function DeviceStatusTable({ devices, loading, showError = false }: DeviceStatusTableProps) {
  if (loading) {
    return (
      <Card>
        <CardContent className="py-6">
          <div className="space-y-2">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </div>
        </CardContent>
      </Card>
    )
  }

  if (devices.length === 0) {
    return (
      <Card>
        <CardContent className="flex flex-col items-center justify-center py-12">
          <Monitor className="mb-4 h-12 w-12 text-muted-foreground" />
          <p className="text-lg font-medium">No devices in this status</p>
        </CardContent>
      </Card>
    )
  }

  return (
    <Card>
      <CardContent className="pt-6">
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Device</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Started</TableHead>
                <TableHead>Completed</TableHead>
                {showError && <TableHead>Error</TableHead>}
              </TableRow>
            </TableHeader>
            <TableBody>
              {devices.map((device) => (
                <TableRow key={device.deviceId}>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <Monitor className="h-4 w-4 text-muted-foreground" />
                      <div className="flex flex-col">
                        <span className="font-medium">{device.deviceName}</span>
                        <span className="text-xs text-muted-foreground font-mono">
                          {device.deviceId.slice(0, 8)}...
                        </span>
                      </div>
                    </div>
                  </TableCell>
                  <TableCell>
                    <DeviceStatusBadge status={device.status} />
                  </TableCell>
                  <TableCell>
                    {device.startedAt ? (
                      <span className="text-sm text-muted-foreground">
                        {formatDistanceToNow(new Date(device.startedAt), { addSuffix: true })}
                      </span>
                    ) : (
                      <span className="text-sm text-muted-foreground">-</span>
                    )}
                  </TableCell>
                  <TableCell>
                    {device.completedAt ? (
                      <span className="text-sm text-muted-foreground">
                        {formatDistanceToNow(new Date(device.completedAt), { addSuffix: true })}
                      </span>
                    ) : (
                      <span className="text-sm text-muted-foreground">-</span>
                    )}
                  </TableCell>
                  {showError && (
                    <TableCell>
                      {device.error ? (
                        <div className="flex items-start gap-2 max-w-md">
                          <AlertCircle className="h-4 w-4 text-red-600 flex-shrink-0 mt-0.5" />
                          <span className="text-sm text-red-600">{device.error}</span>
                        </div>
                      ) : (
                        <span className="text-sm text-muted-foreground">-</span>
                      )}
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      </CardContent>
    </Card>
  )
}

function DeviceStatusBadge({ status }: { status: DeviceRolloutState }) {
  switch (status) {
    case DeviceRolloutState.Pending:
      return (
        <Badge variant="outline" className="gap-1">
          <Clock className="h-3 w-3" />
          Pending
        </Badge>
      )
    case DeviceRolloutState.Updating:
      return (
        <Badge variant="outline" className="gap-1 border-blue-500 text-blue-600">
          <Loader2 className="h-3 w-3 animate-spin" />
          Updating
        </Badge>
      )
    case DeviceRolloutState.Succeeded:
      return (
        <Badge variant="outline" className="gap-1 border-green-500 text-green-600">
          <CheckCircle2 className="h-3 w-3" />
          Succeeded
        </Badge>
      )
    case DeviceRolloutState.Failed:
      return (
        <Badge variant="outline" className="gap-1 border-red-500 text-red-600">
          <XCircle className="h-3 w-3" />
          Failed
        </Badge>
      )
    default:
      return <Badge variant="outline">{status}</Badge>
  }
}
