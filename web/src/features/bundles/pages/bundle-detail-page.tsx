/**
 * Bundle Detail Page
 */

import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { formatDistanceToNow } from 'date-fns'
import { ArrowLeft, Package, GitBranch, CheckCircle2, Users, Edit, Monitor, Calendar, User, Activity, Rocket } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useBundle, useBundleAssignedDevices } from '@/hooks/api/use-bundles'
import { useDevices } from '@/hooks/api/use-devices'
import { useRollouts } from '@/hooks/api/use-rollouts'
import { CreateVersionDialog } from '../components/create-version-dialog'
import { AssignBundleDialog } from '../components/assign-bundle-dialog'
import { CreatePhasedRolloutDialog } from '../components/create-phased-rollout-dialog'
import { BundleVersion, RolloutStatus } from '@/api/types'
import { Clock, Loader2, XCircle } from 'lucide-react'

export function BundleDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: bundle, isLoading, isError } = useBundle(id || '', !!id)
  const { data: assignedDevices, isLoading: assignedDevicesLoading } = useBundleAssignedDevices(
    id || '',
    !!id
  )
  const { data: devicesData } = useDevices({ pageSize: 1000 })
  const { data: rolloutsData, isError: rolloutsError } = useRollouts({
    ...(id && { bundleId: id }),
    pageSize: 10
  })

  const [createVersionOpen, setCreateVersionOpen] = useState(false)
  const [assignBundleOpen, setAssignBundleOpen] = useState(false)
  const [createPhasedRolloutOpen, setCreatePhasedRolloutOpen] = useState(false)

  if (isLoading) {
    return (
      <div className="container mx-auto space-y-6 py-6">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (isError || !bundle) {
    return (
      <div className="container mx-auto py-6">
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12">
            <Package className="mb-4 h-12 w-12 text-muted-foreground" />
            <p className="text-lg font-medium">Bundle not found</p>
            <Button onClick={() => navigate('/bundles')} className="mt-4">
              Back to Bundles
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  const activeVersion = bundle.versions?.find((v) => v.isActive)

  // Join assigned devices with device details
  const devicesWithDetails = assignedDevices?.map((assigned) => {
    const device = devicesData?.data.find((d) => d.id === assigned.deviceId)
    return {
      ...assigned,
      deviceName: device?.name || 'Unknown Device',
      deviceStatus: device?.status || 'unknown',
    }
  })

  const deviceCount = assignedDevices?.length ?? 0

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
            <h1 className="text-3xl font-bold">{bundle.name}</h1>
            {bundle.description && (
              <p className="text-sm text-muted-foreground">{bundle.description}</p>
            )}
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="outline" onClick={() => setAssignBundleOpen(true)}>
            <Users className="mr-2 h-4 w-4" />
            Assign to Devices
          </Button>
          <Button variant="outline" onClick={() => setCreatePhasedRolloutOpen(true)}>
            <Rocket className="mr-2 h-4 w-4" />
            Create Phased Rollout
          </Button>
          <Button onClick={() => setCreateVersionOpen(true)}>
            <GitBranch className="mr-2 h-4 w-4" />
            New Version
          </Button>
        </div>
      </div>

      {/* Bundle Info Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Current Version</CardTitle>
            <GitBranch className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">v{bundle.currentVersion}</div>
            {activeVersion && (
              <p className="text-xs text-muted-foreground">
                Released {formatDistanceToNow(new Date(activeVersion.createdAt), { addSuffix: true })}
              </p>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Versions</CardTitle>
            <Package className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">{bundle.versions?.length || 0}</div>
            <p className="text-xs text-muted-foreground">
              Created {formatDistanceToNow(new Date(bundle.createdAt), { addSuffix: true })}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Deployed Devices</CardTitle>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {assignedDevicesLoading ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <>
                <div className="text-2xl font-bold">{deviceCount}</div>
                <p className="text-xs text-muted-foreground">
                  {deviceCount === 1 ? 'device' : 'devices'} assigned
                </p>
              </>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="versions" className="space-y-4">
        <TabsList>
          <TabsTrigger value="versions">Versions</TabsTrigger>
          <TabsTrigger value="containers">Containers</TabsTrigger>
          {!rolloutsError && (
            <TabsTrigger value="rollouts">
              Rollouts
              {rolloutsData && rolloutsData.data.length > 0 && (
                <Badge variant="secondary" className="ml-2">
                  {rolloutsData.data.length}
                </Badge>
              )}
            </TabsTrigger>
          )}
          <TabsTrigger value="devices">
            Assigned Devices
            {deviceCount > 0 && (
              <Badge variant="secondary" className="ml-2">
                {deviceCount}
              </Badge>
            )}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="versions" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Version History</CardTitle>
              <CardDescription>All versions of this bundle</CardDescription>
            </CardHeader>
            <CardContent>
              {bundle.versions && bundle.versions.length > 0 ? (
                <div className="space-y-4">
                  {bundle.versions
                    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
                    .map((version) => (
                      <VersionCard key={version.version} version={version} />
                    ))}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground py-8 text-center">No versions available</p>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="containers" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Container Definitions</CardTitle>
              <CardDescription>Containers in the current version (v{bundle.currentVersion})</CardDescription>
            </CardHeader>
            <CardContent>
              {activeVersion?.containers && activeVersion.containers.length > 0 ? (
                <div className="space-y-4">
                  {activeVersion.containers.map((container, index) => (
                    <Card key={index}>
                      <CardHeader>
                        <div className="flex items-center justify-between">
                          <CardTitle className="text-base">{container.name}</CardTitle>
                          <Badge variant="outline" className="font-mono">
                            {container.image}:{container.tag}
                          </Badge>
                        </div>
                      </CardHeader>
                      <CardContent className="space-y-2">
                        {container.environment && Object.keys(container.environment).length > 0 && (
                          <div>
                            <p className="text-sm font-medium mb-2">Environment Variables:</p>
                            <div className="rounded-md bg-muted p-3 space-y-1">
                              {Object.entries(container.environment).map(([key, value]) => (
                                <div key={key} className="flex items-center gap-2 text-sm font-mono">
                                  <span className="text-muted-foreground">{key}=</span>
                                  <span>{value}</span>
                                </div>
                              ))}
                            </div>
                          </div>
                        )}
                        {container.ports && container.ports.length > 0 && (
                          <div>
                            <p className="text-sm font-medium mb-2">Port Mappings:</p>
                            <div className="flex flex-wrap gap-2">
                              {container.ports.map((port, i) => (
                                <Badge key={i} variant="secondary">
                                  {port.host}:{port.container} ({port.protocol})
                                </Badge>
                              ))}
                            </div>
                          </div>
                        )}
                        {container.volumes && container.volumes.length > 0 && (
                          <div>
                            <p className="text-sm font-medium mb-2">Volume Mounts:</p>
                            <div className="space-y-1">
                              {container.volumes.map((volume, i) => (
                                <div key={i} className="text-sm font-mono">
                                  {volume.hostPath} → {volume.containerPath}
                                  {volume.readOnly && (
                                    <Badge variant="outline" className="ml-2 text-xs">
                                      read-only
                                    </Badge>
                                  )}
                                </div>
                              ))}
                            </div>
                          </div>
                        )}
                      </CardContent>
                    </Card>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground py-8 text-center">
                  No container definitions available
                </p>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {!rolloutsError && (
          <TabsContent value="rollouts" className="space-y-4">
            <Card>
              <CardHeader>
                <CardTitle>Rollout History</CardTitle>
                <CardDescription>Deployment rollouts for this bundle</CardDescription>
              </CardHeader>
              <CardContent>
                {!rolloutsData || rolloutsData.data.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-12 text-center">
                  <Activity className="mb-4 h-12 w-12 text-muted-foreground" />
                  <p className="text-lg font-medium">No rollouts yet</p>
                  <p className="text-sm text-muted-foreground mb-4">
                    Assign this bundle to devices or groups to create a rollout
                  </p>
                  <Button onClick={() => setAssignBundleOpen(true)}>
                    <Users className="mr-2 h-4 w-4" />
                    Assign to Devices
                  </Button>
                </div>
              ) : (
                <div className="rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Status</TableHead>
                        <TableHead>Version</TableHead>
                        <TableHead>Target</TableHead>
                        <TableHead>Progress</TableHead>
                        <TableHead>Started</TableHead>
                        <TableHead>Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {rolloutsData.data.map((rollout) => (
                        <TableRow key={rollout.id}>
                          <TableCell>
                            <RolloutStatusBadge status={rollout.status} />
                          </TableCell>
                          <TableCell>
                            <Badge variant="outline" className="font-mono">
                              v{rollout.version}
                            </Badge>
                          </TableCell>
                          <TableCell>
                            <div className="flex items-center gap-2">
                              <Users className="h-4 w-4 text-muted-foreground" />
                              <span className="text-sm">
                                {rollout.progress.total} {rollout.targetType === 'group' ? 'groups' : 'devices'}
                              </span>
                            </div>
                          </TableCell>
                          <TableCell>
                            <div className="flex items-center gap-2">
                              <div className="flex gap-1 text-xs">
                                <span className="text-green-600">{rollout.progress.succeeded} ✓</span>
                                <span className="text-blue-600">{rollout.progress.inProgress} ⟳</span>
                                <span className="text-red-600">{rollout.progress.failed} ✗</span>
                                <span className="text-gray-600">{rollout.progress.pending} ○</span>
                              </div>
                            </div>
                          </TableCell>
                          <TableCell>
                            <span className="text-sm text-muted-foreground">
                              {formatDistanceToNow(new Date(rollout.createdAt), { addSuffix: true })}
                            </span>
                          </TableCell>
                          <TableCell>
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => navigate(`/rollouts/${rollout.id}`)}
                            >
                              View Details
                            </Button>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
        )}

        <TabsContent value="devices" className="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle>Assigned Devices</CardTitle>
              <CardDescription>Devices that have this bundle assigned</CardDescription>
            </CardHeader>
            <CardContent>
              {assignedDevicesLoading ? (
                <div className="space-y-2">
                  <Skeleton className="h-12 w-full" />
                  <Skeleton className="h-12 w-full" />
                  <Skeleton className="h-12 w-full" />
                </div>
              ) : !devicesWithDetails || devicesWithDetails.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-12 text-center">
                  <Monitor className="mb-4 h-12 w-12 text-muted-foreground" />
                  <p className="text-lg font-medium">No devices assigned</p>
                  <p className="text-sm text-muted-foreground mb-4">
                    This bundle hasn't been assigned to any devices yet
                  </p>
                  <Button onClick={() => setAssignBundleOpen(true)}>
                    <Users className="mr-2 h-4 w-4" />
                    Assign to Devices
                  </Button>
                </div>
              ) : (
                <div className="rounded-md border">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Device</TableHead>
                        <TableHead>Version</TableHead>
                        <TableHead>Assigned By</TableHead>
                        <TableHead>Assigned</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {devicesWithDetails.map((device) => (
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
                            <Badge variant="outline" className="font-mono">
                              v{device.bundleVersion}
                            </Badge>
                          </TableCell>
                          <TableCell>
                            {device.assignedBy ? (
                              <div className="flex items-center gap-2">
                                <User className="h-3 w-3 text-muted-foreground" />
                                <span className="text-sm">{device.assignedBy}</span>
                              </div>
                            ) : (
                              <span className="text-sm text-muted-foreground">-</span>
                            )}
                          </TableCell>
                          <TableCell>
                            <div className="flex items-center gap-2 text-sm text-muted-foreground">
                              <Calendar className="h-3 w-3" />
                              <span>
                                {formatDistanceToNow(new Date(device.assignedAt), { addSuffix: true })}
                              </span>
                            </div>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      {/* Dialogs */}
      <CreateVersionDialog
        open={createVersionOpen}
        onOpenChange={setCreateVersionOpen}
        bundle={bundle}
      />
      <AssignBundleDialog
        open={assignBundleOpen}
        onOpenChange={setAssignBundleOpen}
        bundle={bundle}
      />
      <CreatePhasedRolloutDialog
        open={createPhasedRolloutOpen}
        onOpenChange={setCreatePhasedRolloutOpen}
        bundle={bundle}
      />
    </div>
  )
}

function VersionCard({ version }: { version: BundleVersion }) {
  return (
    <div className="flex items-start justify-between rounded-lg border p-4">
      <div className="flex items-start gap-3">
        <div className="rounded-full bg-muted p-2">
          {version.isActive ? (
            <CheckCircle2 className="h-4 w-4 text-green-600" />
          ) : (
            <GitBranch className="h-4 w-4 text-muted-foreground" />
          )}
        </div>
        <div>
          <div className="flex items-center gap-2">
            <p className="font-semibold font-mono">v{version.version}</p>
            {version.isActive && (
              <Badge variant="default" className="bg-green-600">
                Active
              </Badge>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            {version.containers.length} container{version.containers.length !== 1 ? 's' : ''}
          </p>
          <p className="text-xs text-muted-foreground">
            Released {formatDistanceToNow(new Date(version.createdAt), { addSuffix: true })}
          </p>
        </div>
      </div>
      {!version.isActive && (
        <Button variant="outline" size="sm">
          <Edit className="mr-2 h-3 w-3" />
          Activate
        </Button>
      )}
    </div>
  )
}

function RolloutStatusBadge({ status }: { status: RolloutStatus }) {
  switch (status) {
    case RolloutStatus.Pending:
      return (
        <Badge variant="outline" className="gap-1">
          <Clock className="h-3 w-3" />
          Pending
        </Badge>
      )
    case RolloutStatus.InProgress:
      return (
        <Badge variant="outline" className="gap-1 border-blue-500 text-blue-600">
          <Loader2 className="h-3 w-3 animate-spin" />
          In Progress
        </Badge>
      )
    case RolloutStatus.Completed:
      return (
        <Badge variant="outline" className="gap-1 border-green-500 text-green-600">
          <CheckCircle2 className="h-3 w-3" />
          Completed
        </Badge>
      )
    case RolloutStatus.Failed:
      return (
        <Badge variant="outline" className="gap-1 border-red-500 text-red-600">
          <XCircle className="h-3 w-3" />
          Failed
        </Badge>
      )
    case RolloutStatus.Cancelled:
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
